using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Inventory;

/// <summary>
/// Lógica común de los formularios de entrada y ajuste de inventario.
/// </summary>
public abstract class InventoryFormPageModel(SupermarketContext context) : PageModel
{
    public SelectList ProductOptions { get; private set; } = null!;

    protected async Task LoadProductOptionsAsync(int? selectedProductId)
    {
        var products = await context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, Label = p.Name + " (stock " + p.Stock + ")" })
            .ToListAsync();

        ProductOptions = new SelectList(products, "Id", "Label", selectedProductId);
    }

    /// <summary>
    /// Traduce el resultado del servicio en una redirección al kardex del producto o en errores del formulario.
    /// </summary>
    protected async Task<IActionResult> HandleResultAsync<T>(ServiceResult<T> result, int productId, string successMessage)
    {
        if (result.Succeeded)
        {
            TempData["StatusMessage"] = successMessage;
            return RedirectToPage("./Index", new { productId });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        await LoadProductOptionsAsync(productId);
        return Page();
    }
}
