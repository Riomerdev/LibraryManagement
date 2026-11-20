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

app.Run();

