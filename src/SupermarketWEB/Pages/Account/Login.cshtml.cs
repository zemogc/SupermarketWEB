using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Authentication)]
public class LoginModel(SupermarketContext context, IPasswordHasher<User> passwordHasher) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = CookieSignIn.NormalizeEmail(Input.Email);
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == email);
        var result = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Input.Password);

        if (user is null || result == PasswordVerificationResult.Failed)
        {
            // Mensaje genérico para no revelar qué correos están registrados.
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return Page();
        }

        // Se informa solo después de verificar la contraseña, para no revelar el estado de cuentas ajenas.
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta está desactivada. Comunícate con un administrador.");
            return Page();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, Input.Password);
            await context.SaveChangesAsync();
        }

        await CookieSignIn.SignInAsync(HttpContext, user, Input.RememberMe);

        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Index");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no es válido.")]
        [Display(Name = "Correo")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Mantener la sesión iniciada")]
        public bool RememberMe { get; set; }
    }
}
