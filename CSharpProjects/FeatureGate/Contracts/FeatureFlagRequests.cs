namespace FeatureGate.Contracts;

public sealed record CreateFeatureFlagRequest(
    string? Key,
    string? Description,
    bool Enabled,
    int RolloutPercentage);

public sealed record UpdateFeatureFlagRequest(
    string? Description,
    bool Enabled,
    int RolloutPercentage);

public sealed record EvaluateFlagRequest(string? FlagKey, string? UserId);

public sealed record FeatureFlagAuditResponse(
    Guid Id,
    Guid FeatureFlagId,
    string FlagKey,
    string Action,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string? BeforeJson,
    string? AfterJson);

public sealed record FeatureFlagSnapshot(
    string Key,
    string Description,
    bool Enabled,
    int RolloutPercentage,
    DateTimeOffset UpdatedAtUtc);
