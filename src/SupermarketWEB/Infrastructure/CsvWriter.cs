using System.Globalization;
using System.Text;

namespace SupermarketWEB.Infrastructure;

/// <summary>
/// Genera CSV según RFC 4180 (separador coma, números y fechas con cultura invariante).
/// </summary>
public static class CsvWriter
{
    private static readonly char[] CharactersRequiringQuotes = [',', '"', '\r', '\n'];
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, header);

        foreach (var row in rows)
        {
            AppendRow(builder, row);
        }

        return builder.ToString();
    }

    public static string FormatField(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            // Solo los textos pueden traer fórmulas; los números se escriben tal cual.
            string s => NeutralizeFormula(s),
            // Montos con dos decimales fijos: SQLite devuelve escalas variables (6800.0, 6800.5).
            decimal amount => amount.ToString("0.00", CultureInfo.InvariantCulture),
            DateTime date => date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => NeutralizeFormula(value.ToString() ?? string.Empty),
        };

        return text.IndexOfAny(CharactersRequiringQuotes) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    /// <summary>
    /// Evita la inyección de fórmulas al abrir el archivo en Excel u otras hojas de cálculo.
    /// </summary>
    private static string NeutralizeFormula(string value) =>
        value.Length > 0 && FormulaPrefixes.Contains(value[0]) ? "'" + value : value;

    private static void AppendRow(StringBuilder builder, IEnumerable<object?> values)
    {
        builder.AppendJoin(',', values.Select(FormatField));
        builder.Append("\r\n");
    }
}
