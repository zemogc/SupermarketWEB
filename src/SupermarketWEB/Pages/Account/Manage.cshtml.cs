using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Account;

public class ManageModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string Email { get; private set; } = string.Empty;

    public string RoleName { get; private set; } = string.Empty;

    public DateTime MemberSinceLocal { get; private set; }

    public int SalesCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user is null)
        {
            return NotFound();
        }

        Input.FullName = user.FullName;
        await LoadAccountInfoAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await context.Users.FindAsync(User.GetUserId());
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadAccountInfoAsync(user);
            return Page();
        }

        user.FullName = Input.FullName.Trim();
        await context.SaveChangesAsync();

        // El nombre viaja en la cookie: se reemite para que el cambio se vea de inmediato.
        var current = await HttpContext.AuthenticateAsync();
        await CookieSignIn.SignInAsync(HttpContext, user, current.Properties?.IsPersistent ?? false);

        TempData["StatusMessage"] = "Perfil actualizado.";
        return RedirectToPage();
    }

    private async Task LoadAccountInfoAsync(User user)
    {
        Email = user.Email;
        RoleName = DisplayFormat.RoleName(user.Role);
        MemberSinceLocal = timeProvider.ToLocal(user.CreatedAtUtc);
        SalesCount = await context.Sales.CountAsync(s => s.UserId == user.Id && s.Status == SaleStatus.Completed);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        [Display(Name = "Nombre completo")]
        public string FullName { get; set; } = string.Empty;
    }
}
