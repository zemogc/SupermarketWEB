using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class AccountTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private const string Password = "ClaveInicial1";

    private async Task<(HttpClient Client, string Email)> RegisterAsync()
    {
        var client = factory.CreateBrowserClient();
        var email = $"cuenta-{Guid.NewGuid():N}@example.com";

        await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Nombre Original",
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password,
        });

        return (client, email);
    }

    private static Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string current, string next, string? confirm = null) =>
        client.SubmitFormAsync("/Account/ChangePassword", new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = current,
            ["Input.NewPassword"] = next,
            ["Input.ConfirmPassword"] = confirm ?? next,
        });

    [Fact]
    public async Task Manage_ShowsAccountInformation()
    {
        var (client, email) = await RegisterAsync();

        var html = await client.GetHtmlAsync("/Account/Manage");

        Assert.Contains(email, html);
        Assert.Contains("Cajero", html);
        Assert.Contains("Nombre Original", html);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameInDatabaseAndNavigation()
    {
        var (client, email) = await RegisterAsync();

        var response = await client.SubmitFormAsync("/Account/Manage", new Dictionary<string, string>
        {
            ["Input.FullName"] = "  Nombre Nuevo  ",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("Nombre Nuevo", await factory.WithContextAsync(db =>
            db.Users.Where(u => u.Email == email).Select(u => u.FullName).SingleAsync()));
        Assert.Contains("Hola, Nombre Nuevo", await client.GetHtmlAsync("/"));
    }

    [Fact]
    public async Task ChangePassword_KeepsCurrentSessionAndClosesOthers()
    {
        var (client, email) = await RegisterAsync();
        var otherDevice = factory.CreateBrowserClient();
        await otherDevice.LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/")).StatusCode);

        var response = await ChangePasswordAsync(client, Password, "ClaveNueva22");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", (await otherDevice.GetAsync("/")).Headers.Location?.ToString());

        var fresh = factory.CreateBrowserClient();
        Assert.Contains("Correo o contraseña incorrectos.", await (await fresh.LoginAsync(email, Password)).ReadHtmlAsync());
        Assert.Equal("/", (await fresh.LoginAsync(email, "ClaveNueva22")).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ShowsError()
    {
        var (client, _) = await RegisterAsync();

        var response = await ChangePasswordAsync(client, "NoEsLaClave1", "ClaveNueva22");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La contraseña actual no es correcta.", await response.ReadHtmlAsync());
    }

    [Theory]
    [InlineData("ClaveInicial1", "ClaveInicial1", "La nueva contraseña debe ser distinta de la actual.")]
    [InlineData("corta", "corta", "La contraseña debe tener entre 8 y 100 caracteres.")]
    [InlineData("ClaveNueva22", "OtraClave33", "Las contraseñas no coinciden.")]
    public async Task ChangePassword_ValidatesNewPassword(string next, string confirm, string expectedError)
    {
        var (client, _) = await RegisterAsync();

        var response = await ChangePasswordAsync(client, Password, next, confirm);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, await response.ReadHtmlAsync());
    }

    [Theory]
    [InlineData("/Account/Manage")]
    [InlineData("/Account/ChangePassword")]
    public async Task AccountPages_RequireSession(string url)
    {
        var response = await factory.CreateBrowserClient().GetAsync(url);

        Assert.StartsWith("http://localhost/Account/Login", response.Headers.Location?.ToString());
    }
}
