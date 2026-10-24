using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Sales;

public class IndexModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    public const int PageSize = 15;

    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public PagedList<SaleRow> Sales { get; private set; } = null!;

    public int CompletedCount { get; private set; }

    public decimal CompletedTotal { get; private set; }

    public async Task OnGetAsync()
    {
        if (From > To)
        {
            (From, To) = (To, From);
        }

        var query = context.Sales.AsNoTracking();

        if (From is { } from)
        {
            var fromUtc = timeProvider.StartOfDayUtc(from);
            query = query.Where(s => s.CreatedAtUtc >= fromUtc);
        }

        if (To is { } to)
        {
            var toUtc = timeProvider.StartOfDayUtc(to.AddDays(1));
            query = query.Where(s => s.CreatedAtUtc < toUtc);
        }

        // SQLite no suma decimales en el servidor: se traen solo los totales de las ventas completadas.
        var completedTotals = await query
            .Where(s => s.Status == SaleStatus.Completed)
            .Select(s => s.Total)
            .ToListAsync();
        CompletedCount = completedTotals.Count;
        CompletedTotal = completedTotals.Sum();

        var rows = query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .Select(s => new SaleRow(
                s.Id,
                s.CreatedAtUtc,
                s.Customer != null ? s.Customer.FirstName + " " + s.Customer.LastName : null,
                s.PayMode!.Name,
                s.Items.Sum(i => i.Quantity),
                s.Total,
                s.Status));

        Sales = await PagedList<SaleRow>.CreateAsync(rows, PageNumber, PageSize);
    }

    public DateTime ToLocal(DateTime utc) => timeProvider.ToLocal(utc);

    public record SaleRow(
        int Id,
        DateTime CreatedAtUtc,
        string? CustomerName,
        string PayModeName,
        int ItemCount,
        decimal Total,
        SaleStatus Status);
}
