using System.ComponentModel.DataAnnotations;

namespace SupermarketWEB.Models;

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20, ErrorMessage = "El documento no puede superar los {1} caracteres.")]
    [RegularExpression("^[0-9A-Za-z-]+$", ErrorMessage = "El documento solo admite letras, números y guiones.")]
    [Display(Name = "Documento")]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    [Display(Name = "Nombres")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede superar los {1} caracteres.")]
    [Display(Name = "Apellidos")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La dirección no puede superar los {1} caracteres.")]
    [Display(Name = "Dirección")]
    public string? Address { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateOnly? Birthday { get; set; }

    [Phone(ErrorMessage = "El teléfono no es válido.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede superar los {1} caracteres.")]
    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
    [Display(Name = "Correo")]
    public string? Email { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
