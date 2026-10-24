using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;

namespace SupermarketWEB.Pages.Categories;

public class IndexModel(SupermarketContext context) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public IReadOnlyList<CategoryRow> Categories { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = context.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        Categories = await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRow(c.Id, c.Name, c.Description, c.Products.Count))
            .ToListAsync();
    }

    public record CategoryRow(int Id, string Name, string? Description, int ProductCount);
}
