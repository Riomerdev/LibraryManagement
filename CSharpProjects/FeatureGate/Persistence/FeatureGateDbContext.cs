using FeatureGate.Domain;
using Microsoft.EntityFrameworkCore;

namespace FeatureGate.Persistence;

public sealed class FeatureGateDbContext : DbContext
{
    public FeatureGateDbContext(
        DbContextOptions<FeatureGateDbContext> options)
        : base(options)
    {
    }

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFlagAudit> FeatureFlagAudits => Set<FeatureFlagAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var featureFlag = modelBuilder.Entity<FeatureFlag>();

        featureFlag.ToTable("feature_flags", table =>
            table.HasCheckConstraint(
                "ck_feature_flags_rollout_percentage",
                "rollout_percentage BETWEEN 0 AND 100"));

        featureFlag.HasKey(flag => flag.Id);

        featureFlag.Property(flag => flag.Id)
            .HasColumnName("id");

        featureFlag.Property(flag => flag.Key)
            .HasColumnName("flag_key")
            .HasMaxLength(100)
            .IsRequired();

        featureFlag.HasIndex(flag => flag.Key)
            .IsUnique();

        featureFlag.Property(flag => flag.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        featureFlag.Property(flag => flag.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        featureFlag.Property(flag => flag.RolloutPercentage)
            .HasColumnName("rollout_percentage")
            .IsRequired();

        featureFlag.Property(flag => flag.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        featureFlag.Property(flag => flag.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        var audit = modelBuilder.Entity<FeatureFlagAudit>();
        audit.ToTable("feature_flag_audits");
        audit.HasKey(item => item.Id);
        audit.Property(item => item.Id).HasColumnName("id");
        audit.Property(item => item.FeatureFlagId).HasColumnName("feature_flag_id");
        audit.Property(item => item.FlagKey).HasColumnName("flag_key").HasMaxLength(100).IsRequired();
        audit.Property(item => item.Action).HasColumnName("action").HasMaxLength(32).IsRequired();
        audit.Property(item => item.Actor).HasColumnName("actor").HasMaxLength(100).IsRequired();
        audit.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        audit.Property(item => item.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
        audit.Property(item => item.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
        audit.HasIndex(item => new { item.FeatureFlagId, item.OccurredAtUtc })
            .HasDatabaseName("ix_feature_flag_audits_flag_occurred");
    }
}
