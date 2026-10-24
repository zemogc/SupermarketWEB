using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Categories;

public class DeleteModel(SupermarketContext context) : PageModel
{
    public Category Category { get; private set; } = new();

    public int ProductCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var category = await context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        Category = category;
        ProductCount = await context.Products.CountAsync(p => p.CategoryId == id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var category = await context.Categories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        if (await context.Products.AnyAsync(p => p.CategoryId == id))
        {
            TempData["ErrorMessage"] = $"No se puede eliminar \"{category.Name}\" porque tiene productos asociados.";
            return RedirectToPage("./Index");
        }

        context.Categories.Remove(category);
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Categoría \"{category.Name}\" eliminada.";
        return RedirectToPage("./Index");
    }
}
