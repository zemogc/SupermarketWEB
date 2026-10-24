using SupermarketWEB.Models;

namespace SupermarketWEB.Infrastructure;

public static class Policies
{
    /// <summary>
    /// Administración del catálogo, precios, usuarios y anulación de ventas.
    /// </summary>
    public const string Admin = "Admin";

    public static readonly string AdminRole = UserRole.Admin.ToString();
}
