namespace SupermarketWEB.Models;

public enum UserRole
{
    Cashier = 0,
    Admin = 1,
}

public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Cashier;

    /// <summary>
    /// Cambia cada vez que se modifican los permisos del usuario. Las sesiones guardan el valor
    /// con el que se emitieron y se invalidan si deja de coincidir.
    /// </summary>
    public string SecurityStamp { get; set; } = NewSecurityStamp();

    public DateTime CreatedAtUtc { get; set; }

    public static string NewSecurityStamp() => Guid.NewGuid().ToString("N");
}
