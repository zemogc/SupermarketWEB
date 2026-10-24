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

    public bool CanCancel => Sale.Status == SaleStatus.Completed && User.IsInRole(Policies.AdminRole);

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        // La página es visible para cajeros, así que la anulación se autoriza en el handler.
        if (!User.IsInRole(Policies.AdminRole))
        {
            return Forbid();
        }

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
