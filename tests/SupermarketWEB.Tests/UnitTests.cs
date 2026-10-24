using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Tests;

public class DisplayFormatTests
{
    [Theory]
    [InlineData(4500, "$ 4.500")]
    [InlineData(3999.5, "$ 3.999,50")]
    [InlineData(0, "$ 0")]
    public void Currency_UsesColombianFormatAndOnlyShowsCentsWhenPresent(decimal value, string expected)
    {
        // es-CO usa espacio no separable entre el símbolo y el valor.
        Assert.Equal(expected, DisplayFormat.Currency(value).Replace(' ', ' '));
    }

    [Fact]
    public void Date_FormatsDayMonthYearOrDash()
    {
        Assert.Equal("07/03/2001", DisplayFormat.Date(new DateOnly(2001, 3, 7)));
        Assert.Equal("—", DisplayFormat.Date(null));
    }
}

public class ProductValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidProduct_HasNoErrors()
    {
        var product = new Product { Name = "Café 500 g", Price = 18900m, Stock = 10, CategoryId = 1 };

        Assert.Empty(Validate(product));
    }

    [Theory]
    [InlineData("", 1000, 1, 1, nameof(Product.Name))]
    [InlineData("Café", 0, 1, 1, nameof(Product.Price))]
    [InlineData("Café", 1000, -1, 1, nameof(Product.Stock))]
    [InlineData("Café", 1000, 1, 0, nameof(Product.CategoryId))]
    public void InvalidProduct_ReportsTheOffendingField(string name, decimal price, int stock, int categoryId, string field)
    {
        var product = new Product { Name = name, Price = price, Stock = stock, CategoryId = categoryId };

        var errors = Validate(product);

        Assert.Contains(errors, e => e.MemberNames.Contains(field));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(Product.LowStockThreshold, true)]
    [InlineData(Product.LowStockThreshold + 1, false)]
    public void IsLowStock_UsesThreshold(int stock, bool expected)
    {
        Assert.Equal(expected, new Product { Stock = stock }.IsLowStock);
    }
}

public sealed class PagedListTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupermarketContext _context;

    public PagedListTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).Options;
        _context = new SupermarketContext(options);
        _context.Database.EnsureCreated();

        _context.PayModes.AddRange(Enumerable.Range(1, 23).Select(i => new PayMode { Name = $"Medio {i:D2}" }));
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_ReturnsRequestedPage()
    {
        var page = await PagedList<PayMode>.CreateAsync(_context.PayModes.OrderBy(p => p.Name), 2, 10);

        Assert.Equal(23, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("Medio 11", page.Items[0].Name);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 3)]
    public async Task CreateAsync_ClampsOutOfRangePages(int requested, int expected)
    {
        var page = await PagedList<PayMode>.CreateAsync(_context.PayModes.OrderBy(p => p.Name), requested, 10);

        Assert.Equal(expected, page.PageNumber);
        Assert.NotEmpty(page.Items);
    }

    [Fact]
    public async Task ContainsIgnoringCaseAsync_MatchesAccentedUppercase()
    {
        _context.PayModes.Add(new PayMode { Name = "Crédito" });
        await _context.SaveChangesAsync();

        Assert.True(await _context.PayModes.Select(p => p.Name).ContainsIgnoringCaseAsync("CRÉDITO"));
        Assert.False(await _context.PayModes.Select(p => p.Name).ContainsIgnoringCaseAsync("Débito"));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
