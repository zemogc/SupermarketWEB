using System.ComponentModel.DataAnnotations;

namespace SupermarketWEB.Models;

public class PayMode
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La observación no puede superar los {1} caracteres.")]
    [Display(Name = "Observación")]
    public string? Observation { get; set; }
}
