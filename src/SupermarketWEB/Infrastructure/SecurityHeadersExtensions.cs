namespace SupermarketWEB.Infrastructure;

public static class SecurityHeadersExtensions
{
    // Todos los recursos se sirven desde el propio sitio; data: cubre los íconos SVG de Bootstrap.
    private const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; object-src 'none'; " +
        "frame-ancestors 'none'; form-action 'self'; base-uri 'self'";

    /// <summary>
    /// Agrega cabeceras de seguridad a todas las respuestas. La CSP se omite en desarrollo
    /// porque la recarga en caliente inyecta scripts y conexiones WebSocket.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool includeContentSecurityPolicy)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (includeContentSecurityPolicy)
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            await next();
        });
    }
}
