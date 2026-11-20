namespace InventoryControl.Domain;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public int OnHand { get; set; }
    public int ReorderPoint { get; set; }
    public int Version { get; set; } = 1;
    public List<StockMovement> Movements { get; set; } = [];
}
