using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Products;

/// <summary>
/// Lógica común de los formularios de creación y edición de productos.
/// </summary>
public abstract class ProductFormPageModel(SupermarketContext context) : PageModel
{
    protected SupermarketContext Context { get; } = context;

    [BindProperty]
    public Product Product { get; set; } = new();

    protected async Task LoadCategoryOptionsAsync()
    {
        var categories = await Context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        ViewData["CategoryOptions"] = new SelectList(categories, nameof(Category.Id), nameof(Category.Name));
    }

    /// <summary>
    /// Copia los campos editables al producto y valida reglas que dependen de la base de datos.
    /// </summary>
    /// <param name="includeInitialStock">
    /// Solo al crear (junto con el costo inicial): después, stock y costo cambian únicamente
    /// mediante movimientos de inventario.
    /// </param>
    protected async Task<bool> TryApplyFormAsync(Product product, bool includeInitialStock)
    {
        var bound = includeInitialStock
            ? await TryUpdateModelAsync(product, nameof(Product), p => p.Name, p => p.Price, p => p.CategoryId, p => p.Stock, p => p.AverageCost)
            : await TryUpdateModelAsync(product, nameof(Product), p => p.Name, p => p.Price, p => p.CategoryId);

        if (!bound)
        {
            return false;
        }

        product.Name = product.Name.Trim();

        if (!await Context.Categories.AnyAsync(c => c.Id == product.CategoryId))
        {
            ModelState.AddModelError("Product.CategoryId", "La categoría seleccionada no existe.");
            return false;
        }

        return true;
    }
}
