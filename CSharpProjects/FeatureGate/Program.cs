using FeatureGate.Endpoints;
using FeatureGate.Persistence;
using FeatureGate.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<FeatureGateDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FeatureGate")));

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(FeatureFlagEndpoints.AdminPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin"));

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "feature-gate"
}))
.AllowAnonymous()
.WithName("GetLiveness");

app.MapGet("/health/ready", async (
    FeatureGateDbContext db,
    CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ready", dependency = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous()
.WithName("GetReadiness");

FeatureFlagEndpoints.Map(app);

app.Run();

public partial class Program { }
