using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;
using SupermarketWEB.Services;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public sealed class CostServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupermarketContext _context;
    private readonly InventoryService _inventory;
    private readonly SaleService _sales;
    private readonly ReportService _reports;
    private readonly Product _coffee;
    private readonly Product _sugar;
    private readonly Supplier _supplier;
    private readonly User _admin;
    private readonly PayMode _cash;

    public CostServiceTests()
    {
        _connection.Open();
        _context = new SupermarketContext(new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var category = new Category { Name = "Despensa" };
        _coffee = new Product { Name = "Café", Price = 20000m, Stock = 0, Category = category };
        _sugar = new Product { Name = "Azúcar", Price = 5000m, Stock = 10, Category = category };
        _supplier = new Supplier { Name = "Distribuidora Andina", TaxId = "900123456-1" };
        _admin = new User { Email = "admin@example.com", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        _cash = new PayMode { Name = "Efectivo" };
        _context.AddRange(_coffee, _sugar, _supplier, _admin, _cash);
        _context.SaveChanges();

        _inventory = new InventoryService(_context, TimeProvider.System);
        _sales = new SaleService(_context, _inventory, TimeProvider.System);
        _reports = new ReportService(_context, TimeProvider.System);
    }

    private Product Reload(Product product)
    {
        using var context = new SupermarketContext(new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).Options);
        return context.Products.AsNoTracking().Single(p => p.Id == product.Id);
    }

    [Theory]
    [InlineData(0, null, 10, 1000.0, 1000.0)]        // sin existencias: el costo es el de la compra
    [InlineData(5, null, 10, 1000.0, 1000.0)]        // sin costo previo: el costo es el de la compra
    [InlineData(10, 1000.0, 10, 1200.0, 1100.0)]       // mitad y mitad
    [InlineData(30, 1000.0, 10, 2000.0, 1250.0)]       // ponderado por cantidad
    [InlineData(3, 1000.0, 0, 5000.0, 1000.0)]         // recibir 0 unidades no cambia el promedio
    public void WeightedAverageCost_WeighsByQuantity(int stock, double? cost, int quantity, double unitCost, double expected)
    {
        var result = InventoryService.WeightedAverageCost(stock, (decimal?)cost, quantity, (decimal)unitCost);

        Assert.Equal((decimal)expected, result);
    }

    [Fact]
    public void WeightedAverageCost_RoundsToFourDecimals()
    {
        Assert.Equal(1033.3333m, InventoryService.WeightedAverageCost(2, 1000m, 1, 1100m));
    }

    [Fact]
    public async Task Receive_WithCost_UpdatesAverageCostAndRecordsSupplier()
    {
        await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, _supplier.Id, 12000m);
        var second = await _inventory.ReceiveAsync(_coffee.Id, 30, "Factura 77", _admin.Id, _supplier.Id, 14000m);

        Assert.True(second.Succeeded);
        Assert.Equal((_supplier.Id, 14000m), (second.Value!.SupplierId!.Value, second.Value.UnitCost!.Value));
        var coffee = Reload(_coffee);
        Assert.Equal(40, coffee.Stock);
        Assert.Equal(13500m, coffee.AverageCost);
        Assert.Equal(0.325m, coffee.Margin);
    }

    [Fact]
    public async Task Receive_WithoutCost_KeepsAverageCost()
    {
        await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, unitCost: 12000m);

        await _inventory.ReceiveAsync(_coffee.Id, 5, "Bonificación", _admin.Id);

        var coffee = Reload(_coffee);
        Assert.Equal((15, 12000m), (coffee.Stock, coffee.AverageCost!.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Receive_WithNonPositiveCost_IsRejected(double cost)
    {
        var result = await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, unitCost: (decimal)cost);

        Assert.False(result.Succeeded);
        Assert.Equal(0, Reload(_coffee).Stock);
    }

    [Fact]
    public async Task Receive_WithUnknownSupplier_IsRejected()
    {
        var result = await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, supplierId: 9999, unitCost: 100m);

        Assert.False(result.Succeeded);
        Assert.Contains("El proveedor seleccionado no existe.", result.Errors);
    }

    [Fact]
    public async Task Sale_SnapshotsCost_SoLaterPurchasesDoNotChangeItsProfit()
    {
        await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, unitCost: 12000m);
        var sale = (await _sales.RegisterAsync(new RegisterSaleRequest(null, _cash.Id, _admin.Id, [new SaleLineRequest(_coffee.Id, 2)]))).Value!;

        await _inventory.ReceiveAsync(_coffee.Id, 8, null, _admin.Id, unitCost: 18000m);

        var item = await _context.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == sale.Id);
        Assert.Equal(12000m, item.UnitCost);
        Assert.Equal(16000m, item.Profit);
        Assert.Equal(15000m, Reload(_coffee).AverageCost); // (8 × 12.000 + 8 × 18.000) / 16
    }

    [Fact]
    public async Task Report_ComputesProfitOnlyOverCostedLines()
    {
        await _inventory.ReceiveAsync(_coffee.Id, 10, null, _admin.Id, unitCost: 15000m);
        // El azúcar no tiene costo registrado.
        await _sales.RegisterAsync(new RegisterSaleRequest(null, _cash.Id, _admin.Id,
            [new SaleLineRequest(_coffee.Id, 2), new SaleLineRequest(_sugar.Id, 4)]));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var report = await _reports.GetSalesReportAsync(today, today);

        Assert.Equal(60000m, report.Summary.Revenue);
        Assert.Equal(40000m, report.Summary.CostedRevenue);
        Assert.Equal(10000m, report.Summary.GrossProfit);
        Assert.Equal(0.25m, report.Summary.Margin);
        Assert.True(report.Summary.HasUncostedRevenue);

        var coffee = report.ByProduct.Single(p => p.ProductName == "Café");
        var sugar = report.ByProduct.Single(p => p.ProductName == "Azúcar");
        Assert.Equal((10000m, 0.25m), (coffee.Profit!.Value, coffee.Margin!.Value));
        Assert.Null(sugar.Profit);
        Assert.Null(sugar.Margin);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

public class SupplierPagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    private static Dictionary<string, string> SupplierForm(string name, string? taxId = null) => new()
    {
        ["Supplier.Name"] = name,
        ["Supplier.TaxId"] = taxId ?? string.Empty,
        ["Supplier.ContactName"] = "Contacto",
        ["Supplier.Phone"] = "6015550100",
        ["Supplier.Email"] = "  Ventas@Proveedor.COM ",
    };

    [Fact]
    public async Task Create_PersistsNormalizedSupplier()
    {
        var client = await CreateAdminClientAsync();
        var name = $"Proveedor {Guid.NewGuid():N}";

        var response = await client.SubmitFormAsync("/Suppliers/Create", SupplierForm(name));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var supplier = await factory.WithContextAsync(db => db.Suppliers.SingleAsync(s => s.Name == name));
        Assert.Equal("ventas@proveedor.com", supplier.Email);
        Assert.Null(supplier.TaxId);
    }

    [Fact]
    public async Task Create_WithDuplicatedNameOrTaxId_ShowsErrors()
    {
        var client = await CreateAdminClientAsync();
        var name = $"Única {Guid.NewGuid():N}";
        var taxId = $"9{Random.Shared.Next(10000000, 99999999)}-1";
        await client.SubmitFormAsync("/Suppliers/Create", SupplierForm(name, taxId));

        var sameName = await client.SubmitFormAsync("/Suppliers/Create", SupplierForm(name.ToUpperInvariant()));
        var sameTaxId = await client.SubmitFormAsync("/Suppliers/Create", SupplierForm($"Otra {Guid.NewGuid():N}", taxId));

        Assert.Contains("Ya existe un proveedor con este nombre.", await sameName.ReadHtmlAsync());
        Assert.Contains("Ya existe un proveedor con este NIT.", await sameTaxId.ReadHtmlAsync());
    }

    [Fact]
    public async Task Receive_FromPage_WithSupplierAndCost_UpdatesAverageCost()
    {
        var client = await CreateAdminClientAsync();
        var name = $"Proveedor {Guid.NewGuid():N}";
        await client.SubmitFormAsync("/Suppliers/Create", SupplierForm(name));
        var supplierId = await factory.WithContextAsync(db => db.Suppliers.Where(s => s.Name == name).Select(s => s.Id).SingleAsync());
        var product = await factory.WithContextAsync(async db =>
        {
            var p = new Product { Name = $"Producto {Guid.NewGuid():N}", Price = 10000m, CategoryId = await db.Categories.Select(c => c.Id).FirstAsync() };
            db.Products.Add(p);
            await db.SaveChangesAsync();
            return p;
        });

        var response = await client.SubmitFormAsync("/Inventory/Receive", new Dictionary<string, string>
        {
            ["Input.ProductId"] = product.Id.ToString(),
            ["Input.SupplierId"] = supplierId.ToString(),
            ["Input.Quantity"] = "20",
            ["Input.UnitCost"] = "7250.50",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(7250.50m, await factory.WithContextAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.AverageCost).SingleAsync()));

        var history = await client.GetHtmlAsync(response.Headers.Location!.OriginalString);
        Assert.Contains(name, history);

        var delete = await client.GetHtmlAsync($"/Suppliers/Delete/{supplierId}");
        Assert.Contains("tiene entradas de mercancía registradas", delete);
    }

    [Fact]
    public async Task Delete_SupplierWithoutReceipts_RemovesIt()
    {
        var client = await CreateAdminClientAsync();
        var name = $"Temporal {Guid.NewGuid():N}";
        await client.SubmitFormAsync("/Suppliers/Create", SupplierForm(name));
        var id = await factory.WithContextAsync(db => db.Suppliers.Where(s => s.Name == name).Select(s => s.Id).SingleAsync());

        var response = await client.SubmitFormAsync($"/Suppliers/Delete/{id}", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await factory.WithContextAsync(db => db.Suppliers.AnyAsync(s => s.Id == id)));
    }

    [Fact]
    public async Task CreateProduct_WithInitialCost_SetsAverageCostAndShowsMargin()
    {
        var client = await CreateAdminClientAsync();
        var name = $"Producto {Guid.NewGuid():N}";

        await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = name,
            ["Product.CategoryId"] = (await factory.WithContextAsync(db => db.Categories.Select(c => c.Id).FirstAsync())).ToString(),
            ["Product.Price"] = "10000",
            ["Product.Stock"] = "5",
            ["Product.AverageCost"] = "8000",
        });

        var product = await factory.WithContextAsync(db => db.Products.AsNoTracking().SingleAsync(p => p.Name == name));
        var movement = await factory.WithContextAsync(db => db.StockMovements.SingleAsync(m => m.ProductId == product.Id));
        Assert.Equal(8000m, product.AverageCost);
        Assert.Equal(8000m, movement.UnitCost);

        var edit = await client.GetHtmlAsync($"/Products/Edit/{product.Id}");
        Assert.Contains("20,0", edit); // margen del 20 % (formato es-CO)
    }
}
