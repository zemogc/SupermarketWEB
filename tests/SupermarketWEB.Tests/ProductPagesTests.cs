using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class ProductPagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    private Task<int> AnyCategoryIdAsync() =>
        factory.WithContextAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

    [Fact]
    public async Task Create_WithDecimalPrice_PersistsExactValue()
    {
        var client = await CreateAuthenticatedClientAsync();
        var name = $"Arroz {Guid.NewGuid():N}";

        var response = await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = name,
            ["Product.CategoryId"] = (await AnyCategoryIdAsync()).ToString(),
            ["Product.Price"] = "3999.50",
            ["Product.Stock"] = "12",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var product = await factory.WithContextAsync(db => db.Products.SingleAsync(p => p.Name == name));
        Assert.Equal(3999.50m, product.Price);
        Assert.Equal(12, product.Stock);
    }

    [Fact]
    public async Task Create_WithInvalidData_ShowsValidationErrors()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = string.Empty,
            ["Product.CategoryId"] = "0",
            ["Product.Price"] = "0",
            ["Product.Stock"] = "-3",
        });

        var html = await response.ReadHtmlAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El nombre es obligatorio.", html);
        Assert.Contains("Selecciona una categoría.", html);
        Assert.Contains("El precio debe estar entre", html);
        Assert.Contains("El stock no puede ser negativo.", html);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_ShowsError()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = "Huérfano",
            ["Product.CategoryId"] = "999999",
            ["Product.Price"] = "100",
            ["Product.Stock"] = "1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La categoría seleccionada no existe.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Edit_IgnoresFieldsOutsideTheForm()
    {
        var client = await CreateAuthenticatedClientAsync();
        var product = await factory.WithContextAsync(db => db.Products.AsNoTracking().FirstAsync());

        var response = await client.SubmitFormAsync($"/Products/Edit/{product.Id}", new Dictionary<string, string>
        {
            ["Product.Id"] = "424242",
            ["Product.Name"] = product.Name,
            ["Product.CategoryId"] = product.CategoryId.ToString(),
            ["Product.Price"] = "100",
            ["Product.Stock"] = product.Stock.ToString(),
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await factory.WithContextAsync(db => db.Products.AnyAsync(p => p.Id == 424242)));
        Assert.Equal(100m, await factory.WithContextAsync(db => db.Products.Where(p => p.Id == product.Id).Select(p => p.Price).SingleAsync()));
    }

    [Fact]
    public async Task Index_WithLowStockFilter_ListsOnlyLowStockProducts()
    {
        var client = await CreateAuthenticatedClientAsync();

        var html = await client.GetHtmlAsync("/Products?LowStock=true");

        Assert.Contains("Yogur natural", html);
        Assert.Contains("Tomate (kg)", html);
        Assert.DoesNotContain("Leche entera 1 L", html);
    }

    [Fact]
    public async Task Index_WithSearch_IsCaseInsensitive()
    {
        var client = await CreateAuthenticatedClientAsync();

        var html = await client.GetHtmlAsync("/Products?Search=LECHE");

        Assert.Contains("Leche entera 1 L", html);
        Assert.DoesNotContain("Pan tajado", html);
    }

    [Fact]
    public async Task Delete_RemovesProduct()
    {
        var client = await CreateAuthenticatedClientAsync();
        var name = $"Para borrar {Guid.NewGuid():N}";
        await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = name,
            ["Product.CategoryId"] = (await AnyCategoryIdAsync()).ToString(),
            ["Product.Price"] = "10",
            ["Product.Stock"] = "1",
        });
        var id = await factory.WithContextAsync(db => db.Products.Where(p => p.Name == name).Select(p => p.Id).SingleAsync());

        var response = await client.SubmitFormAsync($"/Products/Delete/{id}", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await factory.WithContextAsync(db => db.Products.AnyAsync(p => p.Id == id)));
    }
}
