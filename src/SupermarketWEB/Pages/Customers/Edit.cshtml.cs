using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;

namespace SupermarketWEB.Pages.Customers;

public class EditModel(SupermarketContext context) : CustomerFormPageModel(context)
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await Context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        Customer = customer;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var customer = await Context.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        if (!await TryApplyFormAsync(customer))
        {
            return Page();
        }

        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Cliente \"{customer.FullName}\" actualizado.";
        return RedirectToPage("./Index");
    }
}
