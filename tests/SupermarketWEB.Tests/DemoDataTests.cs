using System.Net;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public class DemoDataTests(DemoDataWebFactory factory) : IClassFixture<DemoDataWebFactory>
{
    [Fact]
    public async Task DemoData_GeneratesSalesAcrossThePeriodIncludingCancellations()
    {
        var sales = await factory.WithContextAsync(db => db.Sales.AsNoTracking().ToListAsync());

        Assert.True(sales.Count > 50, $"Se esperaban más de 50 ventas y hay {sales.Count}.");
        Assert.Contains(sales, s => s.Status == SaleStatus.Cancelled);
        Assert.All(sales, s => Assert.True(s.CreatedAtUtc <= DateTime.UtcNow));
        Assert.True(sales.Select(s => s.CreatedAtUtc.Date).Distinct().Count() >= 20);
    }

    [Fact]
    public async Task DemoData_KeepsInventoryConsistent()
    {
        var products = await factory.WithContextAsync(db => db.Products
            .Select(p => new { p.Name, p.Stock, Movements = db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => m.Quantity) })
            .ToListAsync());

        Assert.All(products, p =>
        {
            Assert.True(p.Stock >= 0, $"{p.Name} quedó con stock negativo.");
            Assert.Equal(p.Stock, p.Movements);
        });

        // Algunos productos quedan escasos a propósito para que el panel muestre alertas.
        Assert.Contains(products, p => p.Stock <= Product.LowStockThreshold);
    }

    [Fact]
    public async Task DemoData_KardexIsChronological()
    {
        var movements = await factory.WithContextAsync(db => db.StockMovements.AsNoTracking().ToListAsync());

        // Recorridos en orden de fecha, los saldos deben encadenarse: saldo anterior + cantidad = saldo.
        Assert.All(movements.GroupBy(m => m.ProductId), product =>
        {
            var ordered = product.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).ToList();
            Assert.Equal(StockMovementType.Initial, ordered[0].Type);
            for (var i = 1; i < ordered.Count; i++)
            {
                Assert.Equal(ordered[i - 1].StockAfter + ordered[i].Quantity, ordered[i].StockAfter);
            }
        });
    }

    [Fact]
    public async Task DemoCashier_CanSignInAndSeesOwnSales()
    {
        var client = factory.CreateBrowserClient();

        var login = await client.LoginAsync(DemoDataSeeder.DemoCashierEmail, DemoDataSeeder.DemoCashierPassword);
        var account = await client.GetHtmlAsync("/Account/Manage");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("Cajero", account);
        Assert.DoesNotContain("<dd class=\"col-6 mb-0\">0</dd>", account);
    }

    [Fact]
    public async Task Reports_ShowDemoActivity()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();

        var html = await client.GetHtmlAsync("/Reports");

        Assert.Contains("Productos más vendidos", html);
        Assert.DoesNotContain("No hay ventas completadas", html);
    }
}
