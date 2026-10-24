using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;

namespace SupermarketWEB.Pages.Suppliers;

public class EditModel(SupermarketContext context) : SupplierFormPageModel(context)
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var supplier = await Context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (supplier is null)
        {
            return NotFound();
        }

        Supplier = supplier;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var supplier = await Context.Suppliers.FindAsync(id);
        if (supplier is null)
        {
            return NotFound();
        }

        if (!await TryApplyFormAsync(supplier))
        {
            return Page();
        }

        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Proveedor \"{supplier.Name}\" actualizado.";
        return RedirectToPage("./Index");
    }
}
