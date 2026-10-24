using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;
using SupermarketWEB.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages()
    .AddMvcOptions(options =>
    {
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustBeANumberAccessor(field => $"El campo {field} debe ser un número.");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"El valor \"{value}\" no es válido para {field}.");
        messages.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
        messages.SetMissingBindRequiredValueAccessor(field => $"Falta un valor para {field}.");
        messages.SetValueIsInvalidAccessor(value => $"El valor \"{value}\" no es válido.");
    });

// Los formularios envían números con punto decimal (input type="number"), así que el
// binding usa la cultura invariante sin importar el idioma del servidor.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture);
    options.SupportedCultures = [CultureInfo.InvariantCulture];
    options.SupportedUICultures = [CultureInfo.InvariantCulture];
});

builder.Services.AddDbContext<SupermarketContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SupermarketDB")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SupermarketWEB.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// Todas las páginas requieren sesión salvo las marcadas con [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddAuthenticationRateLimiting(
    builder.Configuration.GetValue("RateLimiting:AuthenticationPermitsPerMinute", 5));

// Las claves que cifran la cookie de sesión deben sobrevivir a reinicios (por ejemplo, en un
// contenedor); si no se configura una ruta se usa la ubicación por defecto del sistema.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("SupermarketWEB")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddHealthChecks().AddDbContextCheck<SupermarketContext>("database");

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Configuration.GetValue<bool>("SeedDemoUser"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSecurityHeaders(includeContentSecurityPolicy: !app.Environment.IsDevelopment());
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();

await app.RunAsync();

// Expuesto para las pruebas de integración con WebApplicationFactory.
public partial class Program;
