using System.Net;
using System.Net.Http.Json;
using FeatureGate.Contracts;
using FeatureGate.Domain;
using FeatureGate.Security;

namespace FeatureGate.Tests;

public sealed class FeatureFlagApiIntegrationTests : IClassFixture<FeatureGateApiFixture>
{
    private readonly FeatureGateApiFixture _fixture;

    public FeatureFlagApiIntegrationTests(FeatureGateApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdminEndpoints_RejectMissingAndInvalidApiKeys()
    {
        using var missingKeyClient = _fixture.Factory.CreateClient();
        using var missingKeyResponse = await missingKeyClient.GetAsync("/api/flags");
        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyResponse.StatusCode);

        using var invalidKeyClient = _fixture.Factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add(
            ApiKeyAuthenticationHandler.HeaderName,
            "not-the-admin-key");
        using var invalidKeyResponse = await invalidKeyClient.GetAsync("/api/flags");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidKeyResponse.StatusCode);
    }

    [Fact]
    public async Task HealthAndEvaluationEndpoints_ArePublic()
    {
        using var client = _fixture.Factory.CreateClient();

        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);

        using var evaluation = await client.PostAsJsonAsync("/api/evaluate", new EvaluateFlagRequest(
            $"missing-{Guid.NewGuid():N}",
            "anonymous-user"));
        Assert.Equal(HttpStatusCode.NotFound, evaluation.StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateReadUpdateAuditAndDeleteFlag()
    {
        using var client = _fixture.CreateAdminClient();
        var key = $"integration-{Guid.NewGuid():N}";

        using var createResponse = await client.PostAsJsonAsync("/api/flags", new CreateFeatureFlagRequest(
            key,
            "Created by API integration test",
            true,
            100));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<FeatureFlag>();
        Assert.NotNull(created);
        Assert.Equal(key, created.Key);
        Assert.NotEqual(DateTimeOffset.MinValue, created.CreatedAtUtc);

        using var readResponse = await client.GetAsync($"/api/flags/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/flags/{created.Id}",
            new UpdateFeatureFlagRequest("Updated description", false, 25));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<FeatureFlag>();
        Assert.NotNull(updated);
        Assert.Equal("Updated description", updated.Description);
        Assert.False(updated.Enabled);
        Assert.Equal(25, updated.RolloutPercentage);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);

        using var auditResponse = await client.GetAsync($"/api/flags/{created.Id}/audit");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var history = await auditResponse.Content.ReadFromJsonAsync<FeatureFlagAuditResponse[]>();
        Assert.NotNull(history);
        Assert.Equal(new[] { "created", "updated" }, history.Select(item => item.Action));
        Assert.All(history, item => Assert.Equal("featuregate-admin", item.Actor));
        Assert.NotNull(history[1].BeforeJson);
        Assert.NotNull(history[1].AfterJson);

        using var deleteResponse = await client.DeleteAsync($"/api/flags/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var deletedRead = await client.GetAsync($"/api/flags/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedRead.StatusCode);

        using var finalAuditResponse = await client.GetAsync($"/api/flags/{created.Id}/audit");
        var finalHistory = await finalAuditResponse.Content
            .ReadFromJsonAsync<FeatureFlagAuditResponse[]>();
        Assert.NotNull(finalHistory);
        Assert.Equal(3, finalHistory.Length);
        Assert.Equal("deleted", finalHistory[^1].Action);
    }

    [Fact]
    public async Task CreateFlag_ValidatesInputAndNormalizesUniqueKey()
    {
        using var client = _fixture.CreateAdminClient();
        var invalid = new CreateFeatureFlagRequest("bad-rollout", "Invalid percentage", true, 101);

        using var invalidResponse = await client.PostAsJsonAsync("/api/flags", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var normalizedKey = $"normalize-{Guid.NewGuid():N}";
        using var createResponse = await client.PostAsJsonAsync("/api/flags", new CreateFeatureFlagRequest(
            $"  {normalizedKey.ToUpperInvariant()}  ",
            "Normalized key",
            true,
            10));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync("/api/flags", new CreateFeatureFlagRequest(
            normalizedKey,
            "Duplicate key",
            true,
            10));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }
}
