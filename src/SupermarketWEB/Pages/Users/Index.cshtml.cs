using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Users;

public class IndexModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<User> Users { get; private set; } = [];

    public int CurrentUserId => User.GetUserId();

    public async Task OnGetAsync()
    {
        Users = await context.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(int id, UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return BadRequest();
        }

        // Impedir el cambio sobre la propia cuenta garantiza que siempre quede un administrador.
        if (id == CurrentUserId)
        {
            TempData["ErrorMessage"] = "No puedes cambiar tu propio rol.";
            return RedirectToPage();
        }

        var user = await context.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role != role)
        {
            user.Role = role;
            user.SecurityStamp = Models.User.NewSecurityStamp(); // cierra sus sesiones abiertas
            await context.SaveChangesAsync();
        }

        TempData["StatusMessage"] = $"{user.FullName} ahora es {DisplayFormat.RoleName(role).ToLowerInvariant()}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetActiveAsync(int id, bool active)
    {
        // Como con el rol, nadie puede desactivarse a sí mismo y siempre queda un administrador activo.
        if (id == CurrentUserId)
        {
            TempData["ErrorMessage"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToPage();
        }

        var user = await context.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsActive != active)
        {
            user.IsActive = active;
            user.SecurityStamp = Models.User.NewSecurityStamp();
            await context.SaveChangesAsync();
        }

        TempData["StatusMessage"] = active
            ? $"{user.FullName} puede volver a iniciar sesión."
            : $"{user.FullName} fue desactivado y se cerraron sus sesiones. Su historial se conserva.";
        return RedirectToPage();
    }

    public DateTime ToLocal(DateTime utc) => timeProvider.ToLocal(utc);
}
