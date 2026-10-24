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

builder.Services.AddRazorPages(options =>
    {
        // Los cajeros registran ventas y clientes; el catálogo, los usuarios y los borrados
        // quedan reservados a administradores.
        var conventions = options.Conventions;
        conventions.AuthorizeFolder("/Categories", Policies.Admin);
        conventions.AuthorizeFolder("/PayModes", Policies.Admin);
        conventions.AuthorizeFolder("/Users", Policies.Admin);
        conventions.AuthorizeFolder("/Reports", Policies.Admin);
        conventions.AuthorizeFolder("/Inventory", Policies.Admin);
        conventions.AuthorizeFolder("/Suppliers", Policies.Admin);
        conventions.AuthorizePage("/Products/Create", Policies.Admin);
        conventions.AuthorizePage("/Products/Edit", Policies.Admin);
        conventions.AuthorizePage("/Products/Delete", Policies.Admin);
        conventions.AuthorizePage("/Customers/Delete", Policies.Admin);
    })
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
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SupermarketWEB.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnValidatePrincipal = CookieSignIn.ValidatePrincipalAsync;
    });

// Todas las páginas requieren sesión salvo las marcadas con [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.Admin, policy => policy.RequireRole(Policies.AdminRole));

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

await DbInitializer.InitializeAsync(
    app.Services,
    seedDemoUser: app.Configuration.GetValue<bool>("SeedDemoUser"),
    seedDemoData: app.Configuration.GetValue<bool>("SeedDemoData"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSecurityHeaders(includeContentSecurityPolicy: !app.Environment.IsDevelopment());

// Detrás de un proxy que termina TLS (por ejemplo, en Docker) la app solo escucha HTTP.
if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}

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
