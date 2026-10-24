using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.PayModes;

/// <summary>
/// Lógica común de los formularios de creación y edición de medios de pago.
/// </summary>
public abstract class PayModeFormPageModel(SupermarketContext context) : PageModel
{
    protected SupermarketContext Context { get; } = context;

    [BindProperty]
    public PayMode PayMode { get; set; } = new();

    protected async Task<bool> TryApplyFormAsync(PayMode payMode)
    {
        if (!await TryUpdateModelAsync(payMode, nameof(PayMode), p => p.Name, p => p.Observation))
        {
            return false;
        }

        payMode.Name = payMode.Name.Trim();

        var duplicated = await Context.PayModes
            .AnyAsync(p => p.Id != payMode.Id && p.Name.ToLower() == payMode.Name.ToLower());

        if (duplicated)
        {
            ModelState.AddModelError("PayMode.Name", "Ya existe un medio de pago con este nombre.");
            return false;
        }

        return true;
    }
}
