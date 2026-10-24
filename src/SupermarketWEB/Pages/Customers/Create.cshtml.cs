using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Customers;

public class CreateModel(SupermarketContext context) : CustomerFormPageModel(context)
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var customer = new Customer();
        if (!await TryApplyFormAsync(customer))
        {
            return Page();
        }

        Context.Customers.Add(customer);
        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Cliente \"{customer.FullName}\" creado.";
        return RedirectToPage("./Index");
    }
}
