using InventoryControl.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Sku).HasMaxLength(40).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(160).IsRequired();
            entity.Property(product => product.Region).HasMaxLength(24).IsRequired();
            entity.Property(product => product.Version).IsConcurrencyToken();
            entity.HasIndex(product => new { product.Sku, product.Region }).IsUnique();
        });
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.Kind).HasMaxLength(12).IsRequired();
            entity.Property(movement => movement.Reason).HasMaxLength(240).IsRequired();
            entity.Property(movement => movement.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(movement => movement.IdempotencyKey).IsUnique();
            entity.HasIndex(movement => new { movement.ProductId, movement.OccurredAt });
            entity.HasOne(movement => movement.Product).WithMany(product => product.Movements)
                .HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
