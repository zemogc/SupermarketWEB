using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Products;

public class IndexModel(SupermarketContext context) : PageModel
{
    public const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool LowStock { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public PagedList<Product> Products { get; private set; } = null!;

    public SelectList CategoryOptions { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var query = context.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }

        if (CategoryId is not null)
        {
            query = query.Where(p => p.CategoryId == CategoryId);
        }

        if (LowStock)
        {
            query = query.Where(p => p.Stock <= Product.LowStockThreshold);
        }

        Products = await PagedList<Product>.CreateAsync(query.OrderBy(p => p.Name), PageNumber, PageSize);

        var categories = await context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        CategoryOptions = new SelectList(categories, nameof(Category.Id), nameof(Category.Name), CategoryId);
    }
}
