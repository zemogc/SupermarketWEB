using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;

namespace SupermarketWEB.Pages.Products;

public class EditModel(SupermarketContext context) : ProductFormPageModel(context)
{
    public int CurrentStock { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await Context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        CurrentStock = product.Stock;
        await LoadCategoryOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var product = await Context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        CurrentStock = product.Stock;

        if (!await TryApplyFormAsync(product, includeInitialStock: false))
        {
            await LoadCategoryOptionsAsync();
            return Page();
        }

        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Producto \"{product.Name}\" actualizado.";
        return RedirectToPage("./Index");
    }
}
