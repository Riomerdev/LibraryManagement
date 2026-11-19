using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var path = builder.Configuration["DATA_FILE"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "bookings.json");
builder.Services.AddSingleton(new JsonStore<BookingState>(path));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "booking-engine" }));
app.MapGet("/api/bookings", async (string? resourceId, JsonStore<BookingState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Bookings
        .Where(item => string.IsNullOrWhiteSpace(resourceId) || item.ResourceId == resourceId)
        .OrderBy(item => item.StartAt).ToArray(), ct));

app.MapPost("/api/bookings", async (CreateBooking request, JsonStore<BookingState> store, CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    if (string.IsNullOrWhiteSpace(request.ResourceId) || string.IsNullOrWhiteSpace(request.CustomerId) ||
        request.StartAt.Offset != TimeSpan.Zero || request.EndAt.Offset != TimeSpan.Zero ||
        request.StartAt <= now || request.EndAt <= request.StartAt ||
        request.EndAt - request.StartAt > TimeSpan.FromHours(8))
        return Results.BadRequest(new { error = "Provide resource and customer IDs, future UTC times, and a duration up to 8 hours." });
    var outcome = await store.UpdateAsync(state =>
    {
        state.Bookings.RemoveAll(item => item.Status == "held" && item.HoldUntil <= now);
        var conflict = state.Bookings.Any(item => item.ResourceId == request.ResourceId.Trim() &&
            item.Status is "held" or "confirmed" &&
            item.StartAt < request.EndAt && request.StartAt < item.EndAt);
        if (conflict) return (Booking: (Booking?)null, Error: "The selected time overlaps another active booking.");
        var booking = new Booking(Guid.NewGuid(), request.ResourceId.Trim(), request.CustomerId.Trim(),
            request.StartAt, request.EndAt, "held", now.AddMinutes(15), now);
        state.Bookings.Add(booking);
        return (Booking: (Booking?)booking, Error: (string?)null);
    }, ct);
    return outcome.Booking is null
        ? Results.Conflict(new { error = outcome.Error })
        : Results.Created($"/api/bookings/{outcome.Booking.Id}", outcome.Booking);
});

app.MapPost("/api/bookings/{id:guid}/confirm", async (
    Guid id, ConfirmBooking request, JsonStore<BookingState> store, CancellationToken ct) =>
{
    var result = await store.UpdateAsync(state =>
    {
        var index = state.Bookings.FindIndex(item => item.Id == id);
        if (index < 0) return (Booking: (Booking?)null, Error: "Booking not found.");
        var booking = state.Bookings[index];
        if (booking.CustomerId != request.CustomerId) return (Booking: (Booking?)null, Error: "Customer does not own this booking.");
        if (booking.Status != "held" || booking.HoldUntil <= DateTimeOffset.UtcNow)
            return (Booking: (Booking?)null, Error: "Hold has expired or is no longer active.");
        state.Bookings[index] = booking with { Status = "confirmed" };
        return (Booking: (Booking?)state.Bookings[index], Error: (string?)null);
    }, ct);
    return result.Booking is null ? Results.Conflict(new { error = result.Error }) : Results.Ok(result.Booking);
});
app.MapPost("/api/bookings/{id:guid}/cancel", async (
    Guid id, ConfirmBooking request, JsonStore<BookingState> store, CancellationToken ct) =>
{
    var result = await store.UpdateAsync(state =>
    {
        var index = state.Bookings.FindIndex(item => item.Id == id);
        if (index < 0) return (Booking: (Booking?)null, Error: "Booking not found.");
        var booking = state.Bookings[index];
        if (booking.CustomerId != request.CustomerId) return (Booking: (Booking?)null, Error: "Customer does not own this booking.");
        if (booking.Status is not ("held" or "confirmed")) return (Booking: (Booking?)null, Error: "Booking cannot be cancelled.");
        state.Bookings[index] = booking with { Status = "cancelled" };
        return (Booking: (Booking?)state.Bookings[index], Error: (string?)null);
    }, ct);
    return result.Booking is null ? Results.Conflict(new { error = result.Error }) : Results.Ok(result.Booking);
});
app.Run();

public sealed class BookingState { public List<Booking> Bookings { get; set; } = []; }
public sealed record CreateBooking(string ResourceId, string CustomerId, DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record ConfirmBooking(string CustomerId);
public sealed record Booking(Guid Id, string ResourceId, string CustomerId, DateTimeOffset StartAt,
    DateTimeOffset EndAt, string Status, DateTimeOffset HoldUntil, DateTimeOffset CreatedAt);