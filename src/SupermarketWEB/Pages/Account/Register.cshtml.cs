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
public class RegisterModel(SupermarketContext context, IPasswordHasher<User> passwordHasher) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = CookieSignIn.NormalizeEmail(Input.Email);
        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError("Input.Email", "Ya existe una cuenta con este correo.");
            return Page();
        }

        // La primera cuenta de una instalación nueva administra el sistema; el resto entra como cajero.
        var isFirstUser = !await context.Users.AnyAsync();

        var user = new User
        {
            Email = email,
            FullName = Input.FullName.Trim(),
            Role = isFirstUser ? UserRole.Admin : UserRole.Cashier,
            CreatedAtUtc = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, Input.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        await CookieSignIn.SignInAsync(HttpContext, user, isPersistent: false);
        TempData["StatusMessage"] = $"Bienvenido, {user.FullName}.";

        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Index");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
        [Display(Name = "Nombre completo")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no es válido.")]
        [StringLength(150, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
        [Display(Name = "Correo")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
