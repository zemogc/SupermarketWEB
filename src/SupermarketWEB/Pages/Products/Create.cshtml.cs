using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Products;

public class CreateModel(SupermarketContext context, InventoryService inventory) : ProductFormPageModel(context)
{
    public async Task OnGetAsync(int? categoryId)
    {
        Product.CategoryId = categoryId ?? 0;
        await LoadCategoryOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var product = new Product();
        if (!await TryApplyFormAsync(product, includeInitialStock: true))
        {
            await LoadCategoryOptionsAsync();
            return Page();
        }

        Context.Products.Add(product);

        if (product.Stock > 0)
        {
            var initial = inventory.CreateMovement(
                product.Id, StockMovementType.Initial, product.Stock, product.Stock, User.GetUserId(), "Stock inicial");
            initial.Product = product;
            Context.StockMovements.Add(initial);
        }

        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Producto \"{product.Name}\" creado.";
        return RedirectToPage("./Index");
    }
}
