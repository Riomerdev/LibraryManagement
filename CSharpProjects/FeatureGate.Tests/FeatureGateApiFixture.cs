using FeatureGate.Persistence;
using FeatureGate.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace FeatureGate.Tests;

public sealed class FeatureGateApiFixture : IAsyncLifetime
{
    public const string AdminApiKey = "integration-test-admin-key-not-secret";

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18.7-alpine3.24").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:FeatureGate"] = _postgres.GetConnectionString(),
            ["FeatureGate:AdminApiKey"] = AdminApiKey
        };

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
                (_, configuration) => configuration.AddInMemoryCollection(settings)));

        var options = new DbContextOptionsBuilder<FeatureGateDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new FeatureGateDbContext(options);
        await db.Database.MigrateAsync();
    }

    public HttpClient CreateAdminClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, AdminApiKey);
        return client;
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return _postgres.DisposeAsync().AsTask();
    }
}
