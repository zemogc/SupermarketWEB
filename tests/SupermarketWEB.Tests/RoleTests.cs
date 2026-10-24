using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Models;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class RoleTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private const string CashierPassword = "ClaveCajero1";

    /// <summary>
    /// Registra una cuenta nueva; como la base ya tiene un administrador, entra como cajero.
    /// </summary>
    private async Task<(HttpClient Client, string Email)> CreateCashierAsync()
    {
        var client = factory.CreateBrowserClient();
        var email = $"cajero-{Guid.NewGuid():N}@example.com";

        var response = await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Cajero de prueba",
            ["Input.Email"] = email,
            ["Input.Password"] = CashierPassword,
            ["Input.ConfirmPassword"] = CashierPassword,
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return (client, email);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    private Task<int> UserIdAsync(string email) =>
        factory.WithContextAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

    [Fact]
    public async Task RegisteredUser_IsCashierWhenAnAdminAlreadyExists()
    {
        var (_, email) = await CreateCashierAsync();

        var role = await factory.WithContextAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Role).SingleAsync());

        Assert.Equal(UserRole.Cashier, role);
    }

    [Theory]
    [InlineData("/Categories")]
    [InlineData("/Categories/Create")]
    [InlineData("/PayModes")]
    [InlineData("/Users")]
    [InlineData("/Products/Create")]
    [InlineData("/Products/Edit/1")]
    [InlineData("/Products/Delete/1")]
    [InlineData("/Customers/Delete/1")]
    public async Task Cashier_IsDeniedAdminPages(string url)
    {
        var (client, _) = await CreateCashierAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/Account/AccessDenied", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Sales")]
    [InlineData("/Sales/Create")]
    [InlineData("/Products")]
    [InlineData("/Customers")]
    [InlineData("/Customers/Create")]
    public async Task Cashier_CanUseDailyOperationPages(string url)
    {
        var (client, _) = await CreateCashierAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cashier_DoesNotSeeAdminNavigationOrActions()
    {
        var (client, _) = await CreateCashierAsync();

        var products = await client.GetHtmlAsync("/Products");

        Assert.DoesNotContain("href=\"/Categories\"", products);
        Assert.DoesNotContain("href=\"/Users\"", products);
        Assert.DoesNotContain("Nuevo producto", products);
        Assert.Contains("Cajero", products);
    }

    [Fact]
    public async Task Cashier_CannotCancelSales()
    {
        var admin = await CreateAdminClientAsync();
        var productId = await factory.WithContextAsync(db => db.Products.Where(p => p.Stock > 0).Select(p => p.Id).FirstAsync());
        var payModeId = await factory.WithContextAsync(db => db.PayModes.Select(p => p.Id).FirstAsync());
        var sale = await admin.SubmitFormAsync("/Sales/Create", new Dictionary<string, string>
        {
            ["Input.PayModeId"] = payModeId.ToString(),
            ["Input.Lines[0].ProductId"] = productId.ToString(),
            ["Input.Lines[0].Quantity"] = "1",
        });
        var location = sale.Headers.Location!.OriginalString;
        var saleId = int.Parse(location.Split('/').Last());

        var (cashier, _) = await CreateCashierAsync();
        var receipt = await cashier.GetHtmlAsync(location);
        var response = await cashier.SubmitFormAsync($"{location}?handler=Cancel", new Dictionary<string, string>(), formPageUrl: location);

        Assert.DoesNotContain("Anular venta", receipt);
        Assert.StartsWith("http://localhost/Account/AccessDenied", response.Headers.Location?.ToString());
        Assert.Equal(SaleStatus.Completed, await factory.WithContextAsync(db =>
            db.Sales.Where(s => s.Id == saleId).Select(s => s.Status).SingleAsync()));
    }

    [Fact]
    public async Task Admin_PromotingUser_EndsTheirSessionAndGrantsAccessOnNextLogin()
    {
        var (cashier, email) = await CreateCashierAsync();
        var admin = await CreateAdminClientAsync();
        var userId = await UserIdAsync(email);

        var change = await admin.SubmitFormAsync($"/Users?handler=ChangeRole&id={userId}", new Dictionary<string, string>
        {
            ["role"] = nameof(UserRole.Admin),
        }, formPageUrl: "/Users");

        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);

        // La cookie anterior quedó invalidada por el cambio de sello de seguridad.
        var staleSession = await cashier.GetAsync("/Sales");
        Assert.StartsWith("http://localhost/Account/Login", staleSession.Headers.Location?.ToString());

        await cashier.LoginAsync(email, CashierPassword);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync("/Categories")).StatusCode);
    }

    [Fact]
    public async Task Admin_DemotingUser_RemovesAdminAccessImmediately()
    {
        var (client, email) = await CreateCashierAsync();
        var userId = await UserIdAsync(email);
        await factory.WithContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.Role = UserRole.Admin;
            user.SecurityStamp = User.NewSecurityStamp();
            return await db.SaveChangesAsync();
        });
        await client.LoginAsync(email, CashierPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Categories")).StatusCode);

        var admin = await CreateAdminClientAsync();
        await admin.SubmitFormAsync($"/Users?handler=ChangeRole&id={userId}", new Dictionary<string, string>
        {
            ["role"] = nameof(UserRole.Cashier),
        }, formPageUrl: "/Users");

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Categories")).StatusCode);
        Assert.Equal(UserRole.Cashier, await factory.WithContextAsync(db =>
            db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync()));
    }

    [Fact]
    public async Task Admin_CannotChangeOwnRole()
    {
        var admin = await CreateAdminClientAsync();
        var adminId = await UserIdAsync("admin@supermarket.local");

        await admin.SubmitFormAsync($"/Users?handler=ChangeRole&id={adminId}", new Dictionary<string, string>
        {
            ["role"] = nameof(UserRole.Cashier),
        }, formPageUrl: "/Users");

        Assert.Equal(UserRole.Admin, await factory.WithContextAsync(db =>
            db.Users.Where(u => u.Id == adminId).Select(u => u.Role).SingleAsync()));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Users")).StatusCode);
    }
}

public class FirstUserTests(EmptyUsersWebFactory factory) : IClassFixture<EmptyUsersWebFactory>
{
    [Fact]
    public async Task FirstRegisteredUser_BecomesAdmin()
    {
        var client = factory.CreateBrowserClient();

        await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Dueño",
            ["Input.Email"] = "duenio@example.com",
            ["Input.Password"] = "ClaveSegura1",
            ["Input.ConfirmPassword"] = "ClaveSegura1",
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Users")).StatusCode);
    }
}
