using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.PayModes;

public class CreateModel(SupermarketContext context) : PayModeFormPageModel(context)
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var payMode = new PayMode();
        if (!await TryApplyFormAsync(payMode))
        {
            return Page();
        }

        Context.PayModes.Add(payMode);
        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Medio de pago \"{payMode.Name}\" creado.";
        return RedirectToPage("./Index");
    }
}
