using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Suppliers;

public class IndexModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<SupplierRow> Suppliers { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Suppliers = await context.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierRow(
                s.Id,
                s.Name,
                s.TaxId,
                s.ContactName,
                s.Phone,
                context.StockMovements.Count(m => m.SupplierId == s.Id),
                context.StockMovements.Where(m => m.SupplierId == s.Id).Max(m => (DateTime?)m.CreatedAtUtc)))
            .ToListAsync();
    }

    public DateTime ToLocal(DateTime utc) => timeProvider.ToLocal(utc);

    public record SupplierRow(
        int Id, string Name, string? TaxId, string? ContactName, string? Phone, int ReceiptCount, DateTime? LastReceiptUtc);
}
