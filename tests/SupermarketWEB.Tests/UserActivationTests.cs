using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class UserActivationTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private const string Password = "ClaveCajero1";

    private async Task<(HttpClient Client, string Email, int Id)> CreateCashierAsync()
    {
        var client = factory.CreateBrowserClient();
        var email = $"baja-{Guid.NewGuid():N}@example.com";
        await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Cajero temporal",
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password,
        });

        var id = await factory.WithContextAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (client, email, id);
    }

    private async Task<HttpResponseMessage> SetActiveAsync(int userId, bool active)
    {
        var admin = factory.CreateBrowserClient();
        await admin.LoginAsync();
        return await admin.SubmitFormAsync(
            $"/Users?handler=SetActive&id={userId}&active={active}", new Dictionary<string, string>(), formPageUrl: "/Users");
    }

    [Fact]
    public async Task Deactivate_EndsSessionAndBlocksLogin()
    {
        var (cashier, email, id) = await CreateCashierAsync();
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/Sales")).StatusCode);

        var response = await SetActiveAsync(id, active: false);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", (await cashier.GetAsync("/Sales")).Headers.Location?.ToString());

        var login = await factory.CreateBrowserClient().LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Tu cuenta está desactivada.", await login.ReadHtmlAsync());
    }

    [Fact]
    public async Task DeactivatedAccount_WithWrongPassword_ShowsGenericError()
    {
        var (_, email, id) = await CreateCashierAsync();
        await SetActiveAsync(id, active: false);

        var login = await factory.CreateBrowserClient().LoginAsync(email, "OtraClave99");

        // No se revela que la cuenta existe y está desactivada si la contraseña no es correcta.
        var html = await login.ReadHtmlAsync();
        Assert.Contains("Correo o contraseña incorrectos.", html);
        Assert.DoesNotContain("desactivada", html);
    }

    [Fact]
    public async Task Reactivate_AllowsLoginAgain()
    {
        var (_, email, id) = await CreateCashierAsync();
        await SetActiveAsync(id, active: false);

        await SetActiveAsync(id, active: true);
        var login = await factory.CreateBrowserClient().LoginAsync(email, Password);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    [Fact]
    public async Task Deactivate_KeepsSalesHistory()
    {
        var (cashier, _, id) = await CreateCashierAsync();
        var productId = await factory.WithContextAsync(db => db.Products.Where(p => p.Stock > 0).Select(p => p.Id).FirstAsync());
        var payModeId = await factory.WithContextAsync(db => db.PayModes.Select(p => p.Id).FirstAsync());
        await cashier.SubmitFormAsync("/Sales/Create", new Dictionary<string, string>
        {
            ["Input.PayModeId"] = payModeId.ToString(),
            ["Input.Lines[0].ProductId"] = productId.ToString(),
            ["Input.Lines[0].Quantity"] = "1",
        });

        await SetActiveAsync(id, active: false);

        Assert.Equal(1, await factory.WithContextAsync(db => db.Sales.CountAsync(s => s.UserId == id)));
    }

    [Fact]
    public async Task Admin_CannotDeactivateOwnAccount()
    {
        var adminId = await factory.WithContextAsync(db => db.Users.Where(u => u.Email == "admin@supermarket.local").Select(u => u.Id).SingleAsync());

        await SetActiveAsync(adminId, active: false);

        Assert.True(await factory.WithContextAsync(db => db.Users.Where(u => u.Id == adminId).Select(u => u.IsActive).SingleAsync()));
    }
}
