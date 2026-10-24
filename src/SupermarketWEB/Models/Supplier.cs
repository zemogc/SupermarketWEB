using System.ComponentModel.DataAnnotations;

namespace SupermarketWEB.Models;

public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El NIT no puede superar los {1} caracteres.")]
    [RegularExpression("^[0-9-]+$", ErrorMessage = "El NIT solo admite números y guiones.")]
    [Display(Name = "NIT")]
    public string? TaxId { get; set; }

    [StringLength(100, ErrorMessage = "El contacto no puede superar los {1} caracteres.")]
    [Display(Name = "Persona de contacto")]
    public string? ContactName { get; set; }

    [Phone(ErrorMessage = "El teléfono no es válido.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede superar los {1} caracteres.")]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
    [Display(Name = "Correo")]
    public string? Email { get; set; }
}
