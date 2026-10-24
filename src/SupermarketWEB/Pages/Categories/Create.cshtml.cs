using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Categories;

public class CreateModel(SupermarketContext context) : PageModel
{
    [BindProperty]
    public Category Category { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var category = new Category();

        // Solo se aceptan los campos editables para evitar overposting.
        if (!await TryUpdateModelAsync(category, nameof(Category), c => c.Name, c => c.Description))
        {
            return Page();
        }

        category.Name = category.Name.Trim();

        if (await context.Categories.Select(c => c.Name).ContainsIgnoringCaseAsync(category.Name))
        {
            ModelState.AddModelError("Category.Name", "Ya existe una categoría con este nombre.");
            return Page();
        }

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Categoría \"{category.Name}\" creada.";
        return RedirectToPage("./Index");
    }
}
