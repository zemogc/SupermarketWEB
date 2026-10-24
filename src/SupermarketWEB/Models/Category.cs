using System.ComponentModel.DataAnnotations;

namespace SupermarketWEB.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede superar los {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
