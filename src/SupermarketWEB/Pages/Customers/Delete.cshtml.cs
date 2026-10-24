using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Customers;

public class DeleteModel(SupermarketContext context) : PageModel
{
    public Customer Customer { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        Customer = customer;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var customer = await context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        context.Customers.Remove(customer);
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Cliente \"{customer.FullName}\" eliminado.";
        return RedirectToPage("./Index");
    }
}
