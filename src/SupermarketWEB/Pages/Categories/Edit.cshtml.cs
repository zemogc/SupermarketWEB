using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Categories;

public class EditModel(SupermarketContext context) : PageModel
{
    [BindProperty]
    public Category Category { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var category = await context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        Category = category;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var category = await context.Categories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        if (!await TryUpdateModelAsync(category, nameof(Category), c => c.Name, c => c.Description))
        {
            return Page();
        }

        category.Name = category.Name.Trim();

        if (await context.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == category.Name.ToLower()))
        {
            ModelState.AddModelError("Category.Name", "Ya existe una categoría con este nombre.");
            return Page();
        }

        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Categoría \"{category.Name}\" actualizada.";
        return RedirectToPage("./Index");
    }
}
