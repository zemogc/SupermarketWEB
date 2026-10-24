using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Sales;

public class DetailsModel(SupermarketContext context, SaleService saleService, TimeProvider timeProvider) : PageModel
{
    public Sale Sale { get; private set; } = null!;

    public DateTime CreatedAtLocal => timeProvider.ToLocal(Sale.CreatedAtUtc);

    public DateTime? CancelledAtLocal => Sale.CancelledAtUtc is { } cancelled ? timeProvider.ToLocal(cancelled) : null;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var sale = await context.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Customer)
            .Include(s => s.PayMode)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale is null)
        {
            return NotFound();
        }

        Sale = sale;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var result = await saleService.CancelAsync(id);

        if (result.Succeeded)
        {
            TempData["StatusMessage"] = $"Venta N.º {DisplayFormat.SaleNumber(id)} anulada. El stock fue devuelto al inventario.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors);
        }

        return RedirectToPage(new { id });
    }
}
