using Microsoft.EntityFrameworkCore;

namespace SupermarketWEB.Infrastructure;

public static class QueryableNameExtensions
{
    /// <summary>
    /// Indica si algún nombre coincide sin distinguir mayúsculas, incluidas letras con tilde.
    /// </summary>
    /// <remarks>
    /// lower() y COLLATE NOCASE de SQLite solo cubren ASCII ("Á" != "á"), por eso la comparación
    /// se hace en memoria. Es aceptable para tablas de catálogo, que son pequeñas.
    /// </remarks>
    public static async Task<bool> ContainsIgnoringCaseAsync(this IQueryable<string> names, string name)
    {
        var existing = await names.ToListAsync();
        return existing.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}
