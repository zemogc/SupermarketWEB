using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;

namespace SupermarketWEB.Pages.PayModes;

public class EditModel(SupermarketContext context) : PayModeFormPageModel(context)
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var payMode = await Context.PayModes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (payMode is null)
        {
            return NotFound();
        }

        PayMode = payMode;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var payMode = await Context.PayModes.FindAsync(id);
        if (payMode is null)
        {
            return NotFound();
        }

        if (!await TryApplyFormAsync(payMode))
        {
            return Page();
        }

        await Context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Medio de pago \"{payMode.Name}\" actualizado.";
        return RedirectToPage("./Index");
    }
}
