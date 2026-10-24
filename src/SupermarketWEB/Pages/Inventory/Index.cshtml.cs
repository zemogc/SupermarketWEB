using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Inventory;

public class IndexModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    public const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public int? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public StockMovementType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public PagedList<MovementRow> Movements { get; private set; } = null!;

    public SelectList ProductOptions { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var query = context.StockMovements.AsNoTracking();

        if (ProductId is not null)
        {
            query = query.Where(m => m.ProductId == ProductId);
        }

        if (Type is not null)
        {
            query = query.Where(m => m.Type == Type);
        }

        var rows = query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Select(m => new MovementRow(
                m.Id,
                m.CreatedAtUtc,
                m.ProductId,
                m.Product!.Name,
                m.Type,
                m.Quantity,
                m.StockAfter,
                m.Note,
                m.SaleId,
                m.User != null ? m.User.FullName : null,
                m.Supplier != null ? m.Supplier.Name : null,
                m.UnitCost));

        Movements = await PagedList<MovementRow>.CreateAsync(rows, PageNumber, PageSize);

        var products = await context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        ProductOptions = new SelectList(products, nameof(Product.Id), nameof(Product.Name), ProductId);
    }

    public DateTime ToLocal(DateTime utc) => timeProvider.ToLocal(utc);

    public static string TypeName(StockMovementType type) => type switch
    {
        StockMovementType.Initial => "Stock inicial",
        StockMovementType.Receipt => "Entrada",
        StockMovementType.Adjustment => "Ajuste",
        StockMovementType.Sale => "Venta",
        StockMovementType.SaleCancellation => "Anulación de venta",
        _ => type.ToString(),
    };

    public record MovementRow(
        int Id,
        DateTime CreatedAtUtc,
        int ProductId,
        string ProductName,
        StockMovementType Type,
        int Quantity,
        int StockAfter,
        string? Note,
        int? SaleId,
        string? UserName,
        string? SupplierName,
        decimal? UnitCost);
}
