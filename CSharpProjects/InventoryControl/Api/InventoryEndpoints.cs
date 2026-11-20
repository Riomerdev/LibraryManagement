using InventoryControl.Data;
using InventoryControl.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Api;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithOpenApi();
        api.MapGet("/products", async (string? q, string? region, int? page, int? pageSize,
            InventoryDbContext db, CancellationToken ct) =>
        {
            var currentPage = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var query = db.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => EF.Functions.ILike(p.Sku, $"%{term}%")
                    || EF.Functions.ILike(p.Name, $"%{term}%"));
            }
            if (!string.IsNullOrWhiteSpace(region))
                query = query.Where(p => p.Region == region.Trim().ToUpperInvariant());
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(p => p.Region).ThenBy(p => p.Sku)
                .Skip((currentPage - 1) * size).Take(size)
                .Select(p => new ProductResponse(p.Id, p.Sku, p.Name, p.Region, p.OnHand, p.ReorderPoint, p.Version))
                .ToListAsync(ct);
            return Results.Ok(new ProductPage(items, total, currentPage, size));
        }).WithName("ListProducts");

        api.MapPost("/products", async Task<IResult> (CreateProductRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var sku = request.Sku?.Trim().ToUpperInvariant() ?? "";
            var name = request.Name?.Trim() ?? "";
            var region = request.Region?.Trim().ToUpperInvariant() ?? "";
            if (sku.Length is < 2 or > 40 || name.Length is < 2 or > 160
                || region.Length is < 2 or > 24 || request.ReorderPoint < 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["SKU, nombre, región o punto de reorden inválidos."] });
            if (await db.Products.AnyAsync(p => p.Sku == sku && p.Region == region, ct))
                return Results.Conflict(new { error = "Ese SKU ya existe en esa región." });
            var product = new Product { Sku = sku, Name = name, Region = region, ReorderPoint = request.ReorderPoint };
            db.Products.Add(product);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                if (await db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku && p.Region == region, ct))
                    return Results.Conflict(new { error = "Ese SKU ya existe en esa región." });
                throw;
            }
            return Results.Created($"/api/products/{product.Id}", ToResponse(product));
        }).WithName("CreateProduct");

        api.MapPost("/products/{productId:guid}/movements", async Task<IResult> (Guid productId, CreateMovementRequest request,
            HttpRequest http, InventoryDbContext db, CancellationToken ct) =>
        {
            var key = http.Headers["Idempotency-Key"].ToString().Trim();
            var kind = request.Kind?.Trim().ToUpperInvariant() ?? "";
            var reason = request.Reason?.Trim() ?? "";
            if (key.Length is < 8 or > 100 || request.Quantity <= 0
                || kind is not ("RECEIPT" or "ISSUE") || reason.Length is < 3 or > 240)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["movement"] = ["Envía Idempotency-Key, una cantidad positiva, RECEIPT/ISSUE y un motivo."] });

            var previous = await db.Movements.AsNoTracking().SingleOrDefaultAsync(m => m.IdempotencyKey == key, ct);
            if (previous is not null)
            {
                if (previous.ProductId != productId)
                    return Results.Conflict(new { error = "La clave ya se usó para otro producto." });
                return Results.Ok(ToResponse(previous));
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null) return Results.NotFound(new { error = "Producto no encontrado." });
            var delta = kind == "RECEIPT" ? request.Quantity : -request.Quantity;
            if (product.OnHand + delta < 0) return Results.Conflict(new { error = "Inventario insuficiente para esa salida." });
            product.OnHand += delta;
            product.Version++;
            var movement = new StockMovement { ProductId = productId, Kind = kind, QuantityDelta = delta, Reason = reason, IdempotencyKey = key };
            db.Movements.Add(movement);
            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Results.Created($"/api/movements/{movement.Id}", ToResponse(movement));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(ct);
                return Results.Conflict(new { error = "El inventario cambió en paralelo; consulta y vuelve a intentar." });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                var duplicate = await db.Movements.AsNoTracking()
                    .SingleOrDefaultAsync(m => m.IdempotencyKey == key, ct);
                if (duplicate is not null)
                {
                    if (duplicate.ProductId != productId)
                        return Results.Conflict(new { error = "La clave ya se usó para otro producto." });
                    return Results.Ok(ToResponse(duplicate));
                }
                throw;
            }
        }).WithName("CreateStockMovement");
        return app;
    }

    private static ProductResponse ToResponse(Product p) => new(p.Id, p.Sku, p.Name, p.Region, p.OnHand, p.ReorderPoint, p.Version);
    private static MovementResponse ToResponse(StockMovement m) => new(m.Id, m.ProductId, m.Kind, m.QuantityDelta, m.Reason, m.OccurredAt);
}

public sealed record CreateProductRequest(string? Sku, string? Name, string? Region, int ReorderPoint);
public sealed record CreateMovementRequest(string? Kind, int Quantity, string? Reason);
public sealed record ProductResponse(Guid Id, string Sku, string Name, string Region, int OnHand, int ReorderPoint, int Version);
public sealed record ProductPage(IReadOnlyList<ProductResponse> Items, int Total, int Page, int PageSize);
public sealed record MovementResponse(Guid Id, Guid ProductId, string Kind, int QuantityDelta, string Reason, DateTimeOffset OccurredAt);
