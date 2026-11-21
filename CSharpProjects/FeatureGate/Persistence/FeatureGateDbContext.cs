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
    }
}
