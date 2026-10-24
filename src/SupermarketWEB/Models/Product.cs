using System.ComponentModel.DataAnnotations;

namespace SupermarketWEB.Models;

public class Product
{
    public const int LowStockThreshold = 5;

    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "El precio debe estar entre {1} y {2}.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Precio")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    [Display(Name = "Stock")]
    public int Stock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría.")]
    [Display(Name = "Categoría")]
    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>
    /// Costo promedio ponderado de las unidades en inventario; nulo si nunca se registró un costo.
    /// Se recalcula con cada entrada de mercancía que informa costo.
    /// </summary>
    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "El costo debe estar entre {1} y {2}.")]
    [Display(Name = "Costo promedio")]
    public decimal? AverageCost { get; set; }

    public bool IsLowStock => Stock <= LowStockThreshold;

    /// <summary>Margen sobre el precio de venta (0,25 = 25 %), si se conoce el costo.</summary>
    public decimal? Margin => AverageCost is { } cost && Price > 0 ? (Price - cost) / Price : null;
}
