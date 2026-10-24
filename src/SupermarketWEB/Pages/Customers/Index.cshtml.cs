using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Customers;

public class IndexModel(SupermarketContext context) : PageModel
{
    public const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public PagedList<Customer> Customers { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var query = context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(c =>
                c.DocumentNumber.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var ordered = query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName);
        Customers = await PagedList<Customer>.CreateAsync(ordered, PageNumber, PageSize);
    }
}
