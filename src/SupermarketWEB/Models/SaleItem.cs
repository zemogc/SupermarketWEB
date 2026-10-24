namespace SupermarketWEB.Models;

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }

    public Sale? Sale { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>
    /// Nombre y precio se copian al momento de la venta para que el comprobante no cambie
    /// si después se edita el producto.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Costo promedio del producto al momento de vender, para calcular la utilidad histórica.
    /// Nulo si el producto no tenía costo registrado.
    /// </summary>
    public decimal? UnitCost { get; set; }

    public int Quantity { get; set; }

    public decimal Subtotal => UnitPrice * Quantity;

    public decimal? Profit => UnitCost is { } cost ? (UnitPrice - cost) * Quantity : null;
}
