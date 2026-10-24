using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Infrastructure;

public static class CookieSignIn
{
    public const string SecurityStampClaim = "supermarket:security_stamp";

    public static Task SignInAsync(HttpContext httpContext, User user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(SecurityStampClaim, user.SecurityStamp),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties { IsPersistent = isPersistent };

        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }

    /// <summary>
    /// Rechaza la cookie si el usuario ya no existe o si su sello de seguridad cambió
    /// (por ejemplo, al cambiarle el rol), para que los permisos se apliquen de inmediato.
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var stamp = principal?.FindFirstValue(SecurityStampClaim);

        if (principal is null || stamp is null || !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<SupermarketContext>();
        var isCurrent = await db.Users.AnyAsync(u => u.Id == userId && u.SecurityStamp == stamp);

        if (!isCurrent)
        {
            await RejectAsync(context);
        }
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
