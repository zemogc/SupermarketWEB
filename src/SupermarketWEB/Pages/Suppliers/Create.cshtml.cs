using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Suppliers;

public class CreateModel(SupermarketContext context) : SupplierFormPageModel(context)
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var supplier = new Supplier();
        if (!await TryApplyFormAsync(supplier))
        {
            return Page();
        }

        Context.Suppliers.Add(supplier);
        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Proveedor \"{supplier.Name}\" creado.";
        return RedirectToPage("./Index");
    }
}
