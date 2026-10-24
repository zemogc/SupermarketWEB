namespace SupermarketWEB.Models;

public enum StockMovementType
{
    /// <summary>Stock con el que se dio de alta el producto.</summary>
    Initial = 0,

    /// <summary>Entrada de mercancía (compra a proveedor).</summary>
    Receipt = 1,

    /// <summary>Corrección tras un conteo físico (mermas, daños, errores).</summary>
    Adjustment = 2,

    Sale = 3,

    SaleCancellation = 4,
}

/// <summary>
/// Registro inmutable de cada cambio de stock. La suma de las cantidades de un producto
/// es igual a su stock actual.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Variación del stock: positiva para entradas y negativa para salidas.</summary>
    public int Quantity { get; set; }

    public int StockAfter { get; set; }

    public string? Note { get; set; }

    public int? SaleId { get; set; }

    /// <summary>Proveedor de una entrada de mercancía.</summary>
    public int? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Costo unitario de compra informado en una entrada o en el stock inicial.</summary>
    public decimal? UnitCost { get; set; }

    public Sale? Sale { get; set; }

    /// <summary>Usuario que originó el movimiento; nulo para los saldos iniciales cargados por el sistema.</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
