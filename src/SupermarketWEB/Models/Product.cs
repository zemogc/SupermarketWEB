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

    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "El precio debe estar entre {1} y {2}.")]
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

    public bool IsLowStock => Stock <= LowStockThreshold;
}
