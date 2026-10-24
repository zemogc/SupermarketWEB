using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Suppliers;

public class DeleteModel(SupermarketContext context) : PageModel
{
    public Supplier Supplier { get; private set; } = new();

    public bool HasReceipts { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var supplier = await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return NotFound();
        }

        Supplier = supplier;
        HasReceipts = await context.StockMovements.AnyAsync(m => m.SupplierId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var supplier = await context.Suppliers.FindAsync(id);
        if (supplier is null)
        {
            return NotFound();
        }

        if (await context.StockMovements.AnyAsync(m => m.SupplierId == id))
        {
            TempData["ErrorMessage"] = $"No se puede eliminar \"{supplier.Name}\" porque tiene entradas de mercancía registradas.";
            return RedirectToPage("./Index");
        }

        context.Suppliers.Remove(supplier);
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Proveedor \"{supplier.Name}\" eliminado.";
        return RedirectToPage("./Index");
    }
}
