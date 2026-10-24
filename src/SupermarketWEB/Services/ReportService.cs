using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Services;

public record SalesSummary(int SaleCount, int UnitCount, decimal Revenue)
{
    public decimal AverageTicket => SaleCount == 0 ? 0 : Math.Round(Revenue / SaleCount, 2);
}

public record DailySales(DateOnly Day, int SaleCount, decimal Revenue);

public record ProductSales(int ProductId, string ProductName, int Units, decimal Revenue);

public record PayModeSales(string PayModeName, int SaleCount, decimal Revenue);

public record SalesReport(
    DateOnly From,
    DateOnly To,
    SalesSummary Summary,
    IReadOnlyList<DailySales> ByDay,
    IReadOnlyList<ProductSales> ByProduct,
    IReadOnlyList<PayModeSales> ByPayMode);

public record SaleLineExport(
    int SaleId,
    DateTime CreatedAtLocal,
    string CustomerName,
    string PayModeName,
    string CashierName,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

/// <summary>
/// Reportes sobre las ventas completadas de un rango de días locales. Las anuladas no cuentan.
/// </summary>
/// <remarks>
/// SQLite no agrega columnas decimal en el servidor, así que se traen las líneas del periodo
/// (solo las columnas necesarias) y se agrupan en memoria.
/// </remarks>
public class ReportService(SupermarketContext context, TimeProvider timeProvider)
{
    public async Task<SalesReport> GetSalesReportAsync(DateOnly from, DateOnly to)
    {
        var lines = await CompletedSales(from, to)
            .SelectMany(s => s.Items, (s, i) => new
            {
                s.Id,
                s.CreatedAtUtc,
                PayModeName = s.PayMode!.Name,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
            })
            .ToListAsync();

        var sales = lines
            .GroupBy(l => l.Id)
            .Select(g => new
            {
                Day = DateOnly.FromDateTime(timeProvider.ToLocal(g.First().CreatedAtUtc)),
                g.First().PayModeName,
                Revenue = g.Sum(l => l.UnitPrice * l.Quantity),
            })
            .ToList();

        var summary = new SalesSummary(sales.Count, lines.Sum(l => l.Quantity), sales.Sum(s => s.Revenue));

        var byDay = sales
            .GroupBy(s => s.Day)
            .Select(g => new DailySales(g.Key, g.Count(), g.Sum(s => s.Revenue)))
            .OrderBy(d => d.Day)
            .ToList();

        // Se agrupa por producto y se muestra el nombre de su venta más reciente.
        var byProduct = lines
            .GroupBy(l => l.ProductId)
            .Select(g => new ProductSales(
                g.Key,
                g.MaxBy(l => l.CreatedAtUtc)!.ProductName,
                g.Sum(l => l.Quantity),
                g.Sum(l => l.UnitPrice * l.Quantity)))
            .OrderByDescending(p => p.Revenue)
            .ThenBy(p => p.ProductName)
            .ToList();

        var byPayMode = sales
            .GroupBy(s => s.PayModeName)
            .Select(g => new PayModeSales(g.Key, g.Count(), g.Sum(s => s.Revenue)))
            .OrderByDescending(p => p.Revenue)
            .ToList();

        return new SalesReport(from, to, summary, byDay, byProduct, byPayMode);
    }

    public async Task<IReadOnlyList<SaleLineExport>> GetSaleLinesAsync(DateOnly from, DateOnly to)
    {
        var rows = await CompletedSales(from, to)
            .SelectMany(s => s.Items, (s, i) => new
            {
                s.Id,
                s.CreatedAtUtc,
                CustomerName = s.Customer != null ? s.Customer.FirstName + " " + s.Customer.LastName : null,
                PayModeName = s.PayMode!.Name,
                CashierName = s.User!.FullName,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
            })
            .ToListAsync();

        return rows
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .ThenBy(r => r.ProductName)
            .Select(r => new SaleLineExport(
                r.Id,
                timeProvider.ToLocal(r.CreatedAtUtc),
                r.CustomerName ?? "Consumidor final",
                r.PayModeName,
                r.CashierName,
                r.ProductName,
                r.Quantity,
                r.UnitPrice,
                r.UnitPrice * r.Quantity))
            .ToList();
    }

    private IQueryable<Sale> CompletedSales(DateOnly from, DateOnly to)
    {
        var fromUtc = timeProvider.StartOfDayUtc(from);
        var toUtc = timeProvider.StartOfDayUtc(to.AddDays(1));

        return context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAtUtc >= fromUtc && s.CreatedAtUtc < toUtc);
    }
}
