using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class AuthenticationTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Categories")]
    [InlineData("/Products")]
    [InlineData("/Customers")]
    [InlineData("/PayModes")]
    [InlineData("/Products/Create")]
    public async Task ProtectedPages_RedirectAnonymousUsersToLogin(string url)
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Privacy")]
    public async Task PublicPages_AreAccessibleWithoutSession(string url)
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_GrantsAccessToProtectedPages()
    {
        var client = factory.CreateBrowserClient();

        var login = await client.LoginAsync();
        var home = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsGenericError()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.LoginAsync(password: "incorrecta");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Login_WithExternalReturnUrl_RedirectsToHome()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.LoginAsync(url: "/Account/Login?ReturnUrl=https%3A%2F%2Fevil.example.com");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Register_CreatesAccountWithHashedPasswordAndSignsIn()
    {
        var client = factory.CreateBrowserClient();
        var email = $"nuevo-{Guid.NewGuid():N}@example.com";

        var response = await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Usuario Nuevo",
            ["Input.Email"] = email.ToUpperInvariant(),
            ["Input.Password"] = "ClaveSegura1",
            ["Input.ConfirmPassword"] = "ClaveSegura1",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var user = await factory.WithContextAsync(db => db.Users.SingleAsync(u => u.Email == email));
        Assert.NotEqual("ClaveSegura1", user.PasswordHash);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShowsError()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Duplicado",
            ["Input.Email"] = "Admin@Supermarket.local",
            ["Input.Password"] = "ClaveSegura1",
            ["Input.ConfirmPassword"] = "ClaveSegura1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ya existe una cuenta con este correo.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();

        // El formulario de cierre de sesión está en la barra de navegación de cualquier página.
        var response = await client.SubmitFormAsync("/Account/Logout", new Dictionary<string, string>(), formPageUrl: "/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
