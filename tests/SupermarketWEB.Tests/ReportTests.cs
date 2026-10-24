using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;
using SupermarketWEB.Services;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public sealed class ReportServiceTests : IDisposable
{
    // Zona fija UTC-5 (como Colombia) para que los límites de día no dependan de la máquina.
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.CreateCustomTimeZone("Test-05", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupermarketContext _context;
    private readonly ReportService _service;
    private readonly Product _milk;
    private readonly Product _bread;
    private readonly PayMode _cash;
    private readonly PayMode _card;
    private readonly User _cashier;

    public ReportServiceTests()
    {
        _connection.Open();
        _context = new SupermarketContext(new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var category = new Category { Name = "General" };
        _milk = new Product { Name = "Leche", Price = 4000m, Stock = 100, Category = category };
        _bread = new Product { Name = "Pan", Price = 2500m, Stock = 100, Category = category };
        _cash = new PayMode { Name = "Efectivo" };
        _card = new PayMode { Name = "Tarjeta" };
        _cashier = new User { Email = "caja@example.com", FullName = "Caja Uno", PasswordHash = "x" };
        _context.AddRange(_milk, _bread, _cash, _card, _cashier);
        _context.SaveChanges();

        _service = new ReportService(_context, new ZoneTimeProvider(Bogota));
    }

    private void AddSale(DateTime createdAtUtc, PayMode payMode, SaleStatus status, params (Product Product, int Quantity)[] lines)
    {
        var sale = new Sale
        {
            CreatedAtUtc = createdAtUtc,
            PayModeId = payMode.Id,
            UserId = _cashier.Id,
            Status = status,
            Items = lines.Select(l => new SaleItem
            {
                ProductId = l.Product.Id,
                ProductName = l.Product.Name,
                UnitPrice = l.Product.Price,
                Quantity = l.Quantity,
            }).ToList(),
        };
        sale.Total = sale.Items.Sum(i => i.Subtotal);
        _context.Sales.Add(sale);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Report_SummarizesOnlyCompletedSalesInRange()
    {
        AddSale(new DateTime(2026, 3, 9, 15, 0, 0, DateTimeKind.Utc), _cash, SaleStatus.Completed, (_milk, 2), (_bread, 1));
        AddSale(new DateTime(2026, 3, 9, 16, 0, 0, DateTimeKind.Utc), _card, SaleStatus.Completed, (_milk, 1));
        AddSale(new DateTime(2026, 3, 9, 17, 0, 0, DateTimeKind.Utc), _card, SaleStatus.Cancelled, (_bread, 50));
        AddSale(new DateTime(2026, 4, 1, 17, 0, 0, DateTimeKind.Utc), _cash, SaleStatus.Completed, (_bread, 9));

        var report = await _service.GetSalesReportAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(2, report.Summary.SaleCount);
        Assert.Equal(4, report.Summary.UnitCount);
        Assert.Equal(14500m, report.Summary.Revenue);
        Assert.Equal(7250m, report.Summary.AverageTicket);

        Assert.Collection(report.ByProduct,
            p => Assert.Equal(("Leche", 3, 12000m), (p.ProductName, p.Units, p.Revenue)),
            p => Assert.Equal(("Pan", 1, 2500m), (p.ProductName, p.Units, p.Revenue)));

        Assert.Collection(report.ByPayMode,
            p => Assert.Equal(("Efectivo", 1, 10500m), (p.PayModeName, p.SaleCount, p.Revenue)),
            p => Assert.Equal(("Tarjeta", 1, 4000m), (p.PayModeName, p.SaleCount, p.Revenue)));
    }

    [Fact]
    public async Task Report_GroupsByLocalDayNotUtcDay()
    {
        // 03:00 UTC del día 10 son las 22:00 del día 9 en UTC-5.
        AddSale(new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc), _cash, SaleStatus.Completed, (_milk, 1));
        AddSale(new DateTime(2026, 3, 10, 6, 0, 0, DateTimeKind.Utc), _cash, SaleStatus.Completed, (_milk, 1));

        var onlyNinth = await _service.GetSalesReportAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9));
        var both = await _service.GetSalesReportAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 10));

        Assert.Equal(1, onlyNinth.Summary.SaleCount);
        Assert.Collection(both.ByDay,
            d => Assert.Equal(new DateOnly(2026, 3, 9), d.Day),
            d => Assert.Equal(new DateOnly(2026, 3, 10), d.Day));
    }

    [Fact]
    public async Task Report_WithoutSales_ReturnsZeroes()
    {
        var report = await _service.GetSalesReportAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(new SalesSummary(0, 0, 0m), report.Summary);
        Assert.Equal(0m, report.Summary.AverageTicket);
        Assert.Empty(report.ByDay);
    }

    [Fact]
    public async Task SaleLines_IncludeLocalTimeAndConsumerFallback()
    {
        AddSale(new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc), _cash, SaleStatus.Completed, (_bread, 2));

        var line = Assert.Single(await _service.GetSaleLinesAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9)));

        Assert.Equal(new DateTime(2026, 3, 9, 22, 0, 0), line.CreatedAtLocal);
        Assert.Equal("Consumidor final", line.CustomerName);
        Assert.Equal("Caja Uno", line.CashierName);
        Assert.Equal(5000m, line.Subtotal);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class ZoneTimeProvider(TimeZoneInfo zone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => zone;
    }
}

public class CsvWriterTests
{
    [Theory]
    [InlineData("Leche", "Leche")]
    [InlineData("Pan, tajado", "\"Pan, tajado\"")]
    [InlineData("Dice \"hola\"", "\"Dice \"\"hola\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+57 300", "'+57 300")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void FormatField_EscapesTextAndNeutralizesFormulas(string value, string expected)
    {
        Assert.Equal(expected, CsvWriter.FormatField(value));
    }

    [Fact]
    public void FormatField_WritesNumbersAndDatesInvariantly()
    {
        Assert.Equal("3999.50", CsvWriter.FormatField(3999.5m));
        Assert.Equal("6800.00", CsvWriter.FormatField(6800.0m));
        Assert.Equal("-5", CsvWriter.FormatField(-5));
        Assert.Equal("2026-03-09 22:05", CsvWriter.FormatField(new DateTime(2026, 3, 9, 22, 5, 0)));
        Assert.Equal(string.Empty, CsvWriter.FormatField(null));
    }

    [Fact]
    public void Write_UsesCrLfLineEndings()
    {
        var csv = CsvWriter.Write(["A", "B"], [new object?[] { 1, "x" }]);

        Assert.Equal("A,B\r\n1,x\r\n", csv);
    }
}

public class ReportPagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAdminWithSaleAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();

        var productId = await factory.WithContextAsync(db => db.Products.Where(p => p.Name == "Pan tajado").Select(p => p.Id).SingleAsync());
        var payModeId = await factory.WithContextAsync(db => db.PayModes.Where(p => p.Name == "Efectivo").Select(p => p.Id).SingleAsync());
        await client.SubmitFormAsync("/Sales/Create", new Dictionary<string, string>
        {
            ["Input.PayModeId"] = payModeId.ToString(),
            ["Input.Lines[0].ProductId"] = productId.ToString(),
            ["Input.Lines[0].Quantity"] = "2",
        });

        return client;
    }

    [Fact]
    public async Task Report_ShowsTodaySalesForAdmin()
    {
        var client = await CreateAdminWithSaleAsync();

        var html = await client.GetHtmlAsync("/Reports");

        Assert.Contains("Productos más vendidos", html);
        Assert.Contains("Pan tajado", html);
        Assert.Contains("Efectivo", html);
    }

    [Fact]
    public async Task Export_ReturnsCsvWithBomHeaderAndRows()
    {
        var client = await CreateAdminWithSaleAsync();
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        var response = await client.GetAsync($"/Reports?handler=Export&from={today}&to={today}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"ventas_{today.Replace("-", "")}", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.StartsWith("﻿Venta,Fecha,Cliente,Medio de pago,Cajero,Producto,Cantidad,Precio unitario,Subtotal,Costo unitario,Utilidad\r\n", csv);

        // Sin costo registrado, las columnas de costo y utilidad quedan vacías.
        Assert.Contains(",Consumidor final,Efectivo,Administrador,Pan tajado,2,6800.00,13600.00,,\r\n", csv);
    }

    [Fact]
    public async Task Reports_AreDeniedToCashiers()
    {
        var client = factory.CreateBrowserClient();
        await client.SubmitFormAsync("/Account/Register", new Dictionary<string, string>
        {
            ["Input.FullName"] = "Cajero",
            ["Input.Email"] = $"cajero-{Guid.NewGuid():N}@example.com",
            ["Input.Password"] = "ClaveCajero1",
            ["Input.ConfirmPassword"] = "ClaveCajero1",
        });

        var page = await client.GetAsync("/Reports");
        var export = await client.GetAsync("/Reports?handler=Export");

        Assert.StartsWith("http://localhost/Account/AccessDenied", page.Headers.Location?.ToString());
        Assert.StartsWith("http://localhost/Account/AccessDenied", export.Headers.Location?.ToString());
    }
}
