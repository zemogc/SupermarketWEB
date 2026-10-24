using System.Globalization;
using SupermarketWEB.Models;

namespace SupermarketWEB.Infrastructure;

/// <summary>
/// Formatos de presentación. El binding de formularios usa la cultura invariante,
/// así que la cultura local solo se aplica al mostrar valores.
/// </summary>
public static class DisplayFormat
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-CO");

    // Sin decimales para valores enteros (lo habitual en COP); con dos cuando hay centavos.
    public static string Currency(decimal value) =>
        value.ToString(decimal.Truncate(value) == value ? "C0" : "C2", DisplayCulture);

    public static string Number(int value) => value.ToString("N0", DisplayCulture);

    public static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", DisplayCulture) ?? "—";

    public static string DateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", DisplayCulture);

    public static string SaleNumber(int id) => id.ToString("D6", DisplayCulture);

    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        _ => "Cajero",
    };
}
