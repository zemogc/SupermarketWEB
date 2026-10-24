using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Reports;

public class IndexModel(ReportService reportService, TimeProvider timeProvider) : PageModel
{
    private const int DefaultRangeDays = 30;

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    public SalesReport Report { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var (from, to) = ResolveRange();
        Report = await reportService.GetSalesReportAsync(from, to);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var (from, to) = ResolveRange();
        var lines = await reportService.GetSaleLinesAsync(from, to);

        var csv = CsvWriter.Write(
            ["Venta", "Fecha", "Cliente", "Medio de pago", "Cajero", "Producto", "Cantidad", "Precio unitario", "Subtotal"],
            lines.Select(l => new object?[]
            {
                l.SaleId, l.CreatedAtLocal, l.CustomerName, l.PayModeName, l.CashierName,
                l.ProductName, l.Quantity, l.UnitPrice, l.Subtotal,
            }));

        // El BOM UTF-8 hace que Excel reconozca las tildes al abrir el archivo.
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(content, "text/csv; charset=utf-8", $"ventas_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Sin fechas se muestran los últimos 30 días; si vienen invertidas se intercambian.
    /// </summary>
    private (DateOnly From, DateOnly To) ResolveRange()
    {
        var to = To ?? timeProvider.Today();
        var from = From ?? to.AddDays(-(DefaultRangeDays - 1));

        if (from > to)
        {
            (from, to) = (to, from);
        }

        From = from;
        To = to;
        return (from, to);
    }
}
