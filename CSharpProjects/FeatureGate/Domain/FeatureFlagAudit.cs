namespace FeatureGate.Domain;

public sealed class FeatureFlagAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeatureFlagId { get; set; }
    public string FlagKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
