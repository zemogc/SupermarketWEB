using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class CustomerAndPayModePagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    private static Dictionary<string, string> CustomerForm(string document, string? birthday = null) => new()
    {
        ["Customer.DocumentNumber"] = document,
        ["Customer.FirstName"] = "María",
        ["Customer.LastName"] = "López",
        ["Customer.Email"] = "  Maria@Example.COM ",
        ["Customer.Birthday"] = birthday ?? "1990-05-17",
    };

    [Fact]
    public async Task CreateCustomer_NormalizesAndPersistsData()
    {
        var client = await CreateAuthenticatedClientAsync();
        var document = $"cc-{Random.Shared.Next(100000, 999999)}";

        var response = await client.SubmitFormAsync("/Customers/Create", CustomerForm(document));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var customer = await factory.WithContextAsync(db =>
            db.Customers.SingleAsync(c => c.DocumentNumber == document.ToUpperInvariant()));
        Assert.Equal("maria@example.com", customer.Email);
        Assert.Equal(new DateOnly(1990, 5, 17), customer.Birthday);
    }

    [Fact]
    public async Task CreateCustomer_WithExistingDocument_ShowsError()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Customers/Create", CustomerForm("1001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ya existe un cliente con este documento.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task CreateCustomer_WithFutureBirthday_ShowsError()
    {
        var client = await CreateAuthenticatedClientAsync();
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        var response = await client.SubmitFormAsync("/Customers/Create", CustomerForm("FUT-1", tomorrow));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La fecha de nacimiento no puede estar en el futuro.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task CustomerIndex_SearchesByDocumentAndName()
    {
        var client = await CreateAuthenticatedClientAsync();

        var byDocument = await client.GetHtmlAsync("/Customers?Search=1002");
        var byName = await client.GetHtmlAsync("/Customers?Search=ana");

        Assert.Contains("Luis Gómez", byDocument);
        Assert.DoesNotContain("Ana Martínez", byDocument);
        Assert.Contains("Ana Martínez", byName);
    }

    [Fact]
    public async Task CreatePayMode_WithDuplicatedName_ShowsError()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/PayModes/Create", new Dictionary<string, string>
        {
            ["PayMode.Name"] = "TARJETA DÉBITO",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ya existe un medio de pago con este nombre.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task EditPayMode_KeepingItsOwnName_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var payMode = await factory.WithContextAsync(db => db.PayModes.AsNoTracking().FirstAsync(p => p.Name == "Efectivo"));

        var response = await client.SubmitFormAsync($"/PayModes/Edit/{payMode.Id}", new Dictionary<string, string>
        {
            ["PayMode.Name"] = "Efectivo",
            ["PayMode.Observation"] = "Pago en caja o contra entrega",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var observation = await factory.WithContextAsync(db =>
            db.PayModes.Where(p => p.Id == payMode.Id).Select(p => p.Observation).SingleAsync());
        Assert.Equal("Pago en caja o contra entrega", observation);
    }
}
