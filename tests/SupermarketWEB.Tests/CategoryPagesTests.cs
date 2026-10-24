using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Models;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class CategoryPagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    [Fact]
    public async Task Create_WithValidData_PersistsTrimmedCategory()
    {
        var client = await CreateAuthenticatedClientAsync();
        var name = $"Bebidas {Guid.NewGuid():N}";

        var response = await client.SubmitFormAsync("/Categories/Create", new Dictionary<string, string>
        {
            ["Category.Name"] = $"  {name}  ",
            ["Category.Description"] = "Jugos y gaseosas",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await factory.WithContextAsync(db => db.Categories.AnyAsync(c => c.Name == name)));
    }

    [Theory]
    [InlineData("ASEO")]
    [InlineData("LÁCTEOS")]
    public async Task Create_WithDuplicatedNameIgnoringCase_ShowsError(string name)
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Categories/Create", new Dictionary<string, string>
        {
            ["Category.Name"] = name,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ya existe una categoría con este nombre.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Create_WithoutName_ShowsValidationError()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.SubmitFormAsync("/Categories/Create", new Dictionary<string, string>
        {
            ["Category.Name"] = string.Empty,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("El nombre es obligatorio.", await response.ReadHtmlAsync());
    }

    [Fact]
    public async Task Delete_CategoryWithProducts_IsBlocked()
    {
        var client = await CreateAuthenticatedClientAsync();
        var categoryId = await factory.WithContextAsync(db => db.Categories
            .Where(c => c.Products.Any())
            .Select(c => c.Id)
            .FirstAsync());

        await client.SubmitFormAsync($"/Categories/Delete/{categoryId}", new Dictionary<string, string>(), formPageUrl: "/Categories/Create");

        Assert.True(await factory.WithContextAsync(db => db.Categories.AnyAsync(c => c.Id == categoryId)));
    }

    [Fact]
    public async Task Delete_EmptyCategory_RemovesIt()
    {
        var client = await CreateAuthenticatedClientAsync();
        var categoryId = await factory.WithContextAsync(async db =>
        {
            var category = new Category { Name = $"Temporal {Guid.NewGuid():N}" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return category.Id;
        });

        var response = await client.SubmitFormAsync($"/Categories/Delete/{categoryId}", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await factory.WithContextAsync(db => db.Categories.AnyAsync(c => c.Id == categoryId)));
    }

    [Fact]
    public async Task Edit_UnknownCategory_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/Categories/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
