using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.PayModes;

public class DeleteModel(SupermarketContext context) : PageModel
{
    public PayMode PayMode { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var payMode = await context.PayModes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (payMode is null)
        {
            return NotFound();
        }

        PayMode = payMode;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var payMode = await context.PayModes.FindAsync(id);
        if (payMode is null)
        {
            return NotFound();
        }

        context.PayModes.Remove(payMode);
        await context.SaveChangesAsync();

        TempData["StatusMessage"] = $"Medio de pago \"{payMode.Name}\" eliminado.";
        return RedirectToPage("./Index");
    }
}
