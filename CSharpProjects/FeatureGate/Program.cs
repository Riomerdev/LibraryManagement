using FeatureGate.Domain;
using FeatureGate.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

builder.Services.AddDbContext<FeatureGateDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("FeatureGate")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "feature-gate"
}))
.WithName("GetHealth");

app.MapGet("/api/flags", async (
    FeatureGateDbContext db,
    CancellationToken cancellationToken) =>
{
    var flags = await db.FeatureFlags
        .AsNoTracking()
        .OrderBy(flag => flag.Key)
        .ToListAsync(cancellationToken);

    return Results.Ok(flags);
})
.WithName("ListFeatureFlags");

app.MapGet("/api/flags/{id:guid}", async Task<IResult> (
    Guid id,
    FeatureGateDbContext db,
    CancellationToken cancellationToken) =>
{
    var flag = await db.FeatureFlags
        .AsNoTracking()
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (flag is null)
    {
        return Results.NotFound(new { error = "Flag not found." });
    }

    return Results.Ok(flag);
})
.WithName("GetFeatureFlagById");

app.MapPost("/api/flags", async Task<IResult> (
    CreateFeatureFlagRequest request,
    FeatureGateDbContext db,
    CancellationToken cancellationToken) =>
{
    var key = request.Key?.Trim().ToLowerInvariant();
    var description = request.Description?.Trim();

    if (string.IsNullOrWhiteSpace(key))
    {
        return Results.BadRequest(new { error = "key is required." });
    }

    if (key.Length > 100)
    {
        return Results.BadRequest(new { error = "key cannot exceed 100 characters." });
    }

    if (string.IsNullOrWhiteSpace(description))
    {
        return Results.BadRequest(new { error = "description is required." });
    }

    if (description.Length > 500)
    {
        return Results.BadRequest(new { error = "description cannot exceed 500 characters." });
    }

    if (request.RolloutPercentage is < 0 or > 100)
    {
        return Results.BadRequest(new { error = "rolloutPercentage must be between 0 and 100." });
    }

    if (await db.FeatureFlags.AnyAsync(flag => flag.Key == key, cancellationToken))
    {
        return Results.Conflict(new { error = "A flag with this key already exists." });
    }

    var flag = new FeatureFlag
    {
        Key = key,
        Description = description,
        Enabled = request.Enabled,
        RolloutPercentage = request.RolloutPercentage
    };

    db.FeatureFlags.Add(flag);
    await db.SaveChangesAsync(cancellationToken);

    return Results.CreatedAtRoute(
        "GetFeatureFlagById",
        new { id = flag.Id },
        flag);
})
.WithName("CreateFeatureFlag");

app.MapPost("/api/evaluate", async Task<IResult> (
    EvaluateFlagRequest request,
    FeatureGateDbContext db,
    CancellationToken cancellationToken) =>
{
    var key = request.FlagKey?.Trim().ToLowerInvariant();
    var userId = request.UserId?.Trim();

    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new
        {
            error = "flagKey and userId are required."
        });
    }

    var flag = await db.FeatureFlags
        .AsNoTracking()
        .SingleOrDefaultAsync(item => item.Key == key, cancellationToken);

    if (flag is null)
    {
        return Results.NotFound(new { error = "Flag not found." });
    }

    return Results.Ok(new
    {
        flagKey = flag.Key,
        userId,
        enabled = flag.Enabled &&
            FeatureFlagRolloutEvaluator.IsInRollout(flag.Key, userId, flag.RolloutPercentage)
    });
})
.WithName("EvaluateFeature");

app.Run();

record CreateFeatureFlagRequest(
    string? Key,
    string? Description,
    bool Enabled,
    int RolloutPercentage);

record EvaluateFlagRequest(string? FlagKey, string? UserId);
