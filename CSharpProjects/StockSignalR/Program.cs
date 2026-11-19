using Microsoft.AspNetCore.SignalR;
using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var path = builder.Configuration["DATA_FILE"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "inventory.json");
builder.Services.AddSingleton(new JsonStore<InventoryState>(path));
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "stock-signalr" }));
app.MapHub<InventoryHub>("/hubs/inventory");
app.MapGet("/api/stock", async (JsonStore<InventoryState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.OnHand.Select(pair => new
    {
        sku = pair.Key, onHand = pair.Value,
        reserved = state.Reservations.Values.Where(item => item.Sku == pair.Key && item.Status == "active").Sum(item => item.Quantity)
    }).OrderBy(item => item.sku).ToArray(), ct));

app.MapPost("/api/stock/{sku}/receive", async (string sku, ReceiveStock request,
    JsonStore<InventoryState> store, IHubContext<InventoryHub> hub, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(sku) || request.Quantity <= 0)
        return Results.BadRequest(new { error = "SKU and positive quantity are required." });
    var total = await store.UpdateAsync(state =>
    {
        var key = sku.Trim().ToUpperInvariant();
        state.OnHand[key] = checked(state.OnHand.GetValueOrDefault(key) + request.Quantity);
        return state.OnHand[key];
    }, ct);
    await hub.Clients.All.SendAsync("inventory.changed", new { sku = sku.ToUpperInvariant(), onHand = total }, ct);
    return Results.Ok(new { sku = sku.ToUpperInvariant(), onHand = total });
});

app.MapPost("/api/reservations", async (ReserveStock request, JsonStore<InventoryState> store,
    IHubContext<InventoryHub> hub, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.OrderId) || request.Quantity <= 0)
        return Results.BadRequest(new { error = "SKU, orderId and positive quantity are required." });
    var result = await store.UpdateAsync(state =>
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        var existing = state.Reservations.Values.FirstOrDefault(item => item.OrderId == request.OrderId && item.Sku == sku && item.Status == "active");
        if (existing is not null) return (Reservation: existing, Error: (string?)null, Duplicate: true);
        var available = state.OnHand.GetValueOrDefault(sku) -
            state.Reservations.Values.Where(item => item.Sku == sku && item.Status == "active").Sum(item => item.Quantity);
        if (request.Quantity > available) return (Reservation: (Reservation?)null, Error: "Insufficient available stock.", Duplicate: false);
        var reservation = new Reservation(Guid.NewGuid(), sku, request.OrderId.Trim(), request.Quantity, "active", DateTimeOffset.UtcNow);
        state.Reservations[reservation.Id] = reservation;
        return (Reservation: (Reservation?)reservation, Error: (string?)null, Duplicate: false);
    }, ct);
    if (result.Reservation is null) return Results.Conflict(new { error = result.Error });
    await hub.Clients.All.SendAsync("inventory.changed", new { sku = result.Reservation.Sku }, ct);
    return result.Duplicate ? Results.Ok(result.Reservation) : Results.Created($"/api/reservations/{result.Reservation.Id}", result.Reservation);
});

app.MapPost("/api/reservations/{id:guid}/release", async (Guid id, JsonStore<InventoryState> store,
    IHubContext<InventoryHub> hub, CancellationToken ct) =>
{
    var changed = await store.UpdateAsync(state =>
    {
        if (!state.Reservations.TryGetValue(id, out var reservation) || reservation.Status != "active") return false;
        state.Reservations[id] = reservation with { Status = "released" };
        return true;
    }, ct);
    if (!changed) return Results.NotFound(new { error = "Active reservation not found." });
    await hub.Clients.All.SendAsync("inventory.changed", new { reservationId = id }, ct);
    return Results.NoContent();
});
app.Run();

public sealed class InventoryHub : Hub { }
public sealed class InventoryState
{
    public Dictionary<string, int> OnHand { get; set; } = [];
    public Dictionary<Guid, Reservation> Reservations { get; set; } = [];
}
public sealed record ReceiveStock(int Quantity);
public sealed record ReserveStock(string Sku, string OrderId, int Quantity);
public sealed record Reservation(Guid Id, string Sku, string OrderId, int Quantity, string Status, DateTimeOffset CreatedAt);