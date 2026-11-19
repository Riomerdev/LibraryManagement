using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var path = builder.Configuration["DATA_FILE"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "orders.json");
builder.Services.AddSingleton(new JsonStore<OrderState>(path));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
var allowed = new Dictionary<string, string[]>
{
    ["received"] = ["accepted", "cancelled"],
    ["accepted"] = ["packed", "cancelled"],
    ["packed"] = ["shipped", "cancelled"],
    ["shipped"] = ["delivered"],
    ["delivered"] = [],
    ["cancelled"] = []
};
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "order-flow" }));
app.MapGet("/api/orders", async (JsonStore<OrderState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Orders.OrderByDescending(order => order.CreatedAt).ToArray(), ct));
app.MapGet("/api/outbox/pending", async (JsonStore<OrderState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Outbox.Where(item => item.PublishedAt is null).ToArray(), ct));

app.MapPost("/api/orders", async (HttpRequest http, NewOrder request, JsonStore<OrderState> store, CancellationToken ct) =>
{
    var key = http.Headers["Idempotency-Key"].ToString().Trim();
    if (key.Length is < 8 or > 120)
        return Results.BadRequest(new { error = "Send an Idempotency-Key between 8 and 120 characters." });
    if (string.IsNullOrWhiteSpace(request.CustomerId) || request.Items.Count == 0 ||
        request.Items.Any(item => string.IsNullOrWhiteSpace(item.Sku) || item.Quantity <= 0 || item.UnitPriceCents < 0))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["order"] = ["Customer and valid order lines are required."] });
    var outcome = await store.UpdateAsync(state =>
    {
        if (state.Idempotency.TryGetValue("create:" + key, out var existingId))
            return (Order: state.Orders.Single(order => order.Id == existingId), Created: false);
        var now = DateTimeOffset.UtcNow;
        var order = new Order(Guid.NewGuid().ToString("N"), request.CustomerId.Trim(),
            request.Items.Select(line => new OrderLine(line.Sku.Trim(), line.Quantity, line.UnitPriceCents)).ToList(),
            "received", 1, now, [new OrderEvent("received", now)]);
        state.Orders.Add(order);
        state.Idempotency["create:" + key] = order.Id;
        state.Outbox.Add(new OutboxEvent(Guid.NewGuid(), order.Id, "order.received", now, null));
        return (Order: order, Created: true);
    }, ct);
    return outcome.Created ? Results.Created($"/api/orders/{outcome.Order.Id}", outcome.Order) : Results.Ok(outcome.Order);
});

app.MapPost("/api/orders/{id}/transitions", async (
    string id, HttpRequest http, TransitionRequest request, JsonStore<OrderState> store, CancellationToken ct) =>
{
    var key = http.Headers["Idempotency-Key"].ToString().Trim();
    if (key.Length is < 8 or > 120) return Results.BadRequest(new { error = "Idempotency-Key is required." });
    var outcome = await store.UpdateAsync(state =>
    {
        if (state.Idempotency.TryGetValue("transition:" + key, out var existingId))
            return (Order: state.Orders.Single(order => order.Id == existingId), Error: (string?)null);
        var index = state.Orders.FindIndex(order => order.Id == id);
        if (index < 0) return (Order: (Order?)null, Error: "Order not found.");
        var current = state.Orders[index];
        if (request.ExpectedVersion != current.Version)
            return (Order: current, Error: "Version conflict. Reload the order before retrying.");
        if (!allowed.TryGetValue(current.Status, out var next) || !next.Contains(request.ToStatus))
            return (Order: current, Error: $"Transition {current.Status} -> {request.ToStatus} is not allowed.");
        var now = DateTimeOffset.UtcNow;
        var updated = current with { Status = request.ToStatus, Version = current.Version + 1,
            Events = [.. current.Events, new OrderEvent(request.ToStatus, now)] };
        state.Orders[index] = updated;
        state.Outbox.Add(new OutboxEvent(Guid.NewGuid(), id, "order." + request.ToStatus, now, null));
        state.Idempotency["transition:" + key] = id;
        return (Order: updated, Error: (string?)null);
    }, ct);
    if (outcome.Order is null) return Results.NotFound(new { error = outcome.Error });
    if (outcome.Error is not null) return Results.Conflict(new { error = outcome.Error, order = outcome.Order });
    return Results.Ok(outcome.Order);
});
app.Run();

public sealed class OrderState
{
    public List<Order> Orders { get; set; } = [];
    public List<OutboxEvent> Outbox { get; set; } = [];
    public Dictionary<string, string> Idempotency { get; set; } = [];
}
public sealed record OrderLine(string Sku, int Quantity, long UnitPriceCents);
public sealed record NewOrder(string CustomerId, List<OrderLineInput> Items);
public sealed record OrderLineInput(string Sku, int Quantity, long UnitPriceCents);
public sealed record Order(string Id, string CustomerId, List<OrderLine> Items, string Status, int Version,
    DateTimeOffset CreatedAt, List<OrderEvent> Events);
public sealed record OrderEvent(string Status, DateTimeOffset At);
public sealed record OutboxEvent(Guid Id, string OrderId, string Type, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt);
public sealed record TransitionRequest(string ToStatus, int ExpectedVersion);