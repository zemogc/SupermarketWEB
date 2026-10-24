using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.PayModes;

public class IndexModel(SupermarketContext context) : PageModel
{
    public IReadOnlyList<PayMode> PayModes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        PayModes = await context.PayModes.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
    }
}
