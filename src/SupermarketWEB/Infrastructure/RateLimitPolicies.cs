using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SupermarketWEB.Infrastructure;

public static class RateLimitPolicies
{
    public const string Authentication = "authentication";

    /// <summary>
    /// Limita los envíos de formularios de autenticación por IP para frenar ataques de fuerza bruta.
    /// Las peticiones GET no se limitan.
    /// </summary>
    public static IServiceCollection AddAuthenticationRateLimiting(this IServiceCollection services, int permitLimit)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Authentication, httpContext =>
            {
                if (!HttpMethods.IsPost(httpContext.Request.Method))
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });
    }
}
