using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FeatureGate.Domain;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

var flags = new List<FeatureFlag>
{
    new()
    {
        Key = "new-checkout",
        Description = "Nuevo proceso de compra",
        Enabled = true,
        RolloutPercentage = 10
    }
};

app.MapGet("/api/flags", () => flags)
    .WithName("ListFeatureFlags");

app.MapPost("/api/evaluate", IResult (EvaluateFlagRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.FlagKey) ||
        string.IsNullOrWhiteSpace(request.UserId))
    {
        return Results.BadRequest(new
        {
            error = "flagKey and userId are required."
        });
    }

    var flag = flags.SingleOrDefault(item => item.Key == request.FlagKey);

    if (flag is null)
    {
        return Results.NotFound(new { error = "Flag not found." });
    }

    return Results.Ok(new
    {
        flagKey = flag.Key,
        userId = request.UserId,
        enabled = flag.Enabled &&
            IsInRollout(flag.Key, request.UserId!, flag.RolloutPercentage)
    });
})
.WithName("EvaluateFeature");

app.Run();

static bool IsInRollout(string flagKey, string userId, int rolloutPercentage)
{
    var percentage = Math.Clamp(rolloutPercentage, 0, 100);

    if (percentage == 0)
    {
        return false;
    }

    if (percentage == 100)
    {
        return true;
    }

    var input = Encoding.UTF8.GetBytes($"{flagKey}:{userId}");
    var hash = SHA256.HashData(input);
    var bucket = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4)) % 100;

    return bucket < percentage;
}

record EvaluateFlagRequest(string? FlagKey, string? UserId);
