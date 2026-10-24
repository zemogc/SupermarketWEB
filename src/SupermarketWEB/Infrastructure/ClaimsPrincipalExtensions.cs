using System.Security.Claims;

namespace SupermarketWEB.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("El usuario autenticado no tiene un identificador válido.");
    }
}
