using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Account;

// Verifica la contraseña actual, así que se limita igual que el inicio de sesión.
[EnableRateLimiting(RateLimitPolicies.Authentication)]
public class ChangePasswordModel(SupermarketContext context, IPasswordHasher<User> passwordHasher) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await context.Users.FindAsync(User.GetUserId());
        if (user is null)
        {
            return NotFound();
        }

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Input.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Input.CurrentPassword", "La contraseña actual no es correcta.");
            return Page();
        }

        if (Input.NewPassword == Input.CurrentPassword)
        {
            ModelState.AddModelError("Input.NewPassword", "La nueva contraseña debe ser distinta de la actual.");
            return Page();
        }

        user.PasswordHash = passwordHasher.HashPassword(user, Input.NewPassword);
        user.SecurityStamp = Models.User.NewSecurityStamp(); // cierra las demás sesiones
        await context.SaveChangesAsync();

        // La sesión actual se renueva con el sello nuevo para que siga activa.
        var current = await HttpContext.AuthenticateAsync();
        await CookieSignIn.SignInAsync(HttpContext, user, current.Properties?.IsPersistent ?? false);

        TempData["StatusMessage"] = "Contraseña actualizada. Se cerraron las sesiones abiertas en otros dispositivos.";
        return RedirectToPage("./Manage");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu contraseña actual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa la nueva contraseña.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
