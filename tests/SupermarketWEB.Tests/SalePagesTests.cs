using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Models;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class SalePagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    /// <summary>
    /// Crea un producto propio para que cada prueba controle su stock sin interferir con las demás.
    /// </summary>
    private Task<Product> CreateProductAsync(int stock, decimal price = 1000m) =>
        factory.WithContextAsync(async db =>
        {
            var product = new Product
            {
                Name = $"Producto {Guid.NewGuid():N}",
                Price = price,
                Stock = stock,
                CategoryId = await db.Categories.Select(c => c.Id).FirstAsync(),
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product;
        });

    private Task<int> CashPayModeIdAsync() =>
        factory.WithContextAsync(db => db.PayModes.Where(p => p.Name == "Efectivo").Select(p => p.Id).SingleAsync());

    private Task<int> StockOfAsync(int productId) =>
        factory.WithContextAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());

    private async Task<HttpResponseMessage> SubmitSaleAsync(HttpClient client, params (int ProductId, int Quantity)[] lines)
    {
        var fields = new Dictionary<string, string> { ["Input.PayModeId"] = (await CashPayModeIdAsync()).ToString() };
        for (var i = 0; i < lines.Length; i++)
        {
            fields[$"Input.Lines[{i}].ProductId"] = lines[i].ProductId.ToString();
            fields[$"Input.Lines[{i}].Quantity"] = lines[i].Quantity.ToString();
        }

        return await client.SubmitFormAsync("/Sales/Create", fields);
    }

    [Fact]
    public async Task Create_RegistersSaleForCurrentUserAndRedirectsToReceipt()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 10, price: 2500m);

        var response = await SubmitSaleAsync(client, (product.Id, 3));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/Sales/Details/", location);

        var saleId = int.Parse(location.Split('/').Last());
        var sale = await factory.WithContextAsync(db => db.Sales.Include(s => s.User).SingleAsync(s => s.Id == saleId));
        Assert.Equal(7500m, sale.Total);
        Assert.Equal("admin@supermarket.local", sale.User!.Email);
        Assert.Equal(7, await StockOfAsync(product.Id));

        var receipt = await client.GetHtmlAsync(location);
        Assert.Contains(product.Name, receipt);
        Assert.Contains("Consumidor final", receipt);
        Assert.Contains("$ 7.500", receipt.Replace(' ', ' '));
    }

    [Fact]
    public async Task Create_WithInsufficientStock_ShowsErrorAndKeepsStock()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 2);

        var response = await SubmitSaleAsync(client, (product.Id, 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Stock insuficiente", await response.ReadHtmlAsync());
        Assert.Equal(2, await StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Create_WithoutProducts_ShowsError()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Sales/Create", new Dictionary<string, string>
        {
            ["Input.PayModeId"] = (await CashPayModeIdAsync()).ToString(),
            ["Input.Lines[0].ProductId"] = string.Empty,
            ["Input.Lines[0].Quantity"] = "1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Agrega al menos un producto a la venta.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Create_WithoutPayMode_ShowsValidationError()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 5);

        var response = await client.SubmitFormAsync("/Sales/Create", new Dictionary<string, string>
        {
            ["Input.Lines[0].ProductId"] = product.Id.ToString(),
            ["Input.Lines[0].Quantity"] = "1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Selecciona un medio de pago.", await response.ReadHtmlAsync());
        Assert.Equal(5, await StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Cancel_FromReceipt_RestoresStock()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 4);
        var location = (await SubmitSaleAsync(client, (product.Id, 4))).Headers.Location!.OriginalString;
        Assert.Equal(0, await StockOfAsync(product.Id));

        var response = await client.SubmitFormAsync($"{location}?handler=Cancel", new Dictionary<string, string>(), formPageUrl: location);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(4, await StockOfAsync(product.Id));
        Assert.Contains("Anulada", await client.GetHtmlAsync(location));
    }

    [Fact]
    public async Task Index_FiltersByDateRange()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 5);
        await SubmitSaleAsync(client, (product.Id, 1));
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        var todayHtml = await client.GetHtmlAsync($"/Sales?From={today}&To={today}");
        var pastHtml = await client.GetHtmlAsync("/Sales?From=2000-01-01&To=2000-01-31");

        Assert.Contains("Completada", todayHtml);
        Assert.Contains("No hay ventas en el periodo seleccionado.", pastHtml);
    }

    [Fact]
    public async Task DeleteProduct_WithSales_IsBlocked()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 5);
        await SubmitSaleAsync(client, (product.Id, 1));

        var page = await client.GetHtmlAsync($"/Products/Delete/{product.Id}");
        var response = await client.SubmitFormAsync(
            $"/Products/Delete/{product.Id}", new Dictionary<string, string>(), formPageUrl: "/Products/Create");

        Assert.Contains("tiene ventas registradas", page);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await factory.WithContextAsync(db => db.Products.AnyAsync(p => p.Id == product.Id)));
    }

    [Fact]
    public async Task Dashboard_ShowsRecentSales()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await CreateProductAsync(stock: 5);
        var location = (await SubmitSaleAsync(client, (product.Id, 1))).Headers.Location!.OriginalString;
        var saleNumber = int.Parse(location.Split('/').Last()).ToString("D6");

        var html = await client.GetHtmlAsync("/");

        Assert.Contains("Ventas de hoy", html);
        Assert.Contains($"N.º {saleNumber}", html);
    }

    [Fact]
    public async Task Details_UnknownSale_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/Sales/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
