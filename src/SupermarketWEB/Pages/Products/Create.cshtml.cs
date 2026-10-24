using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Products;

public class CreateModel(SupermarketContext context) : ProductFormPageModel(context)
{
    public async Task OnGetAsync(int? categoryId)
    {
        Product.CategoryId = categoryId ?? 0;
        await LoadCategoryOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var product = new Product();
        if (!await TryApplyFormAsync(product))
        {
            await LoadCategoryOptionsAsync();
            return Page();
        }

        Context.Products.Add(product);
        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Producto \"{product.Name}\" creado.";
        return RedirectToPage("./Index");
    }
}
