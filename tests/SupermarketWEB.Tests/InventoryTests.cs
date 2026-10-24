using System.Data.Common;
using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupermarketWEB.Data;
using SupermarketWEB.Models;
using SupermarketWEB.Services;
using SupermarketWEB.Tests.Infrastructure;

namespace SupermarketWEB.Tests;

public sealed class InventoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupermarketContext _context;
    private readonly InventoryService _inventory;
    private readonly Product _rice;
    private readonly User _admin;
    private readonly PayMode _cash;

    public InventoryServiceTests()
    {
        _connection.Open();
        _context = CreateContext();
        _context.Database.EnsureCreated();

        _rice = new Product { Name = "Arroz", Price = 3000m, Stock = 10, Category = new Category { Name = "Granos" } };
        _admin = new User { Email = "admin@example.com", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        _cash = new PayMode { Name = "Efectivo" };
        _context.AddRange(_rice, _admin, _cash);
        _context.SaveChanges();
        _context.StockMovements.Add(new StockMovement
        {
            ProductId = _rice.Id, Type = StockMovementType.Initial, Quantity = 10, StockAfter = 10, CreatedAtUtc = DateTime.UtcNow,
        });
        _context.SaveChanges();

        _inventory = new InventoryService(_context, TimeProvider.System);
    }

    private SupermarketContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options);

    private int CurrentStock()
    {
        using var context = CreateContext();
        return context.Products.Single(p => p.Id == _rice.Id).Stock;
    }

    [Fact]
    public async Task Receive_IncreasesStockAndRecordsMovement()
    {
        var result = await _inventory.ReceiveAsync(_rice.Id, 25, "  Factura 123  ", _admin.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(35, CurrentStock());
        var movement = result.Value!;
        Assert.Equal((StockMovementType.Receipt, 25, 35, "Factura 123", _admin.Id), (movement.Type, movement.Quantity, movement.StockAfter, movement.Note, movement.UserId!.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(InventoryService.MaxQuantity + 1)]
    public async Task Receive_RejectsInvalidQuantities(int quantity)
    {
        var result = await _inventory.ReceiveAsync(_rice.Id, quantity, null, _admin.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(10, CurrentStock());
    }

    [Fact]
    public async Task Receive_ForUnknownProduct_Fails()
    {
        var result = await _inventory.ReceiveAsync(9999, 5, null, _admin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("El producto no existe.", result.Errors);
        Assert.Equal(1, await _context.StockMovements.CountAsync());
    }

    [Theory]
    [InlineData(7, -3)]
    [InlineData(12, 2)]
    [InlineData(0, -10)]
    public async Task Adjust_SetsCountedStockAndRecordsDifference(int counted, int expectedDelta)
    {
        var result = await _inventory.AdjustAsync(_rice.Id, counted, "Conteo mensual", _admin.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(counted, CurrentStock());
        Assert.Equal((StockMovementType.Adjustment, expectedDelta, counted), (result.Value!.Type, result.Value.Quantity, result.Value.StockAfter));
    }

    [Fact]
    public async Task Adjust_WithSameStock_IsRejected()
    {
        var result = await _inventory.AdjustAsync(_rice.Id, 10, "Conteo", _admin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("coincide"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Adjust_RequiresReason(string reason)
    {
        var result = await _inventory.AdjustAsync(_rice.Id, 4, reason, _admin.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(10, CurrentStock());
    }

    [Fact]
    public async Task Adjust_WhenStockChangesDuringCount_DoesNotOverwriteIt()
    {
        // Una venta descuenta 2 unidades entre la lectura del stock y la escritura del ajuste.
        using var racingContext = CreateContext(new ConcurrentSaleInterceptor(_rice.Id, unitsSold: 2));
        var racingInventory = new InventoryService(racingContext, TimeProvider.System);

        var result = await racingInventory.AdjustAsync(_rice.Id, 6, "Conteo", _admin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("cambió mientras"));
        Assert.Equal(8, CurrentStock()); // se conserva la venta concurrente
    }

    [Fact]
    public async Task Movements_AlwaysAddUpToCurrentStock()
    {
        var sales = new SaleService(_context, _inventory, TimeProvider.System);

        await _inventory.ReceiveAsync(_rice.Id, 20, "Compra", _admin.Id);
        var sale = await sales.RegisterAsync(new RegisterSaleRequest(null, _cash.Id, _admin.Id, [new SaleLineRequest(_rice.Id, 7)]));
        await _inventory.AdjustAsync(_rice.Id, 20, "Merma", _admin.Id);
        await sales.CancelAsync(sale.Value!.Id, _admin.Id);

        var movements = await _context.StockMovements
            .Where(m => m.ProductId == _rice.Id)
            .OrderBy(m => m.Id)
            .ToListAsync();

        Assert.Equal(CurrentStock(), movements.Sum(m => m.Quantity));
        Assert.Equal(CurrentStock(), movements[^1].StockAfter);
        Assert.Equal(
            [StockMovementType.Initial, StockMovementType.Receipt, StockMovementType.Sale, StockMovementType.Adjustment, StockMovementType.SaleCancellation],
            movements.Select(m => m.Type));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Descuenta unidades después de que el ajuste leyó el stock y antes de que abra su
    /// transacción, como lo haría una venta confirmada desde otra caja.
    /// </summary>
    private sealed class ConcurrentSaleInterceptor(int productId, int unitsSold) : DbTransactionInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var sale = connection.CreateCommand();
                sale.CommandText = $"UPDATE \"Products\" SET \"Stock\" = \"Stock\" - {unitsSold} WHERE \"Id\" = {productId}";
                await sale.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}

public class InventoryPagesTests(SupermarketWebFactory factory) : IClassFixture<SupermarketWebFactory>
{
    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = factory.CreateBrowserClient();
        await client.LoginAsync();
        return client;
    }

    private Task<int> CategoryIdAsync() => factory.WithContextAsync(db => db.Categories.Select(c => c.Id).FirstAsync());

    private async Task<int> CreateProductAsync(HttpClient client, int initialStock)
    {
        var name = $"Producto {Guid.NewGuid():N}";
        await client.SubmitFormAsync("/Products/Create", new Dictionary<string, string>
        {
            ["Product.Name"] = name,
            ["Product.CategoryId"] = (await CategoryIdAsync()).ToString(),
            ["Product.Price"] = "1000",
            ["Product.Stock"] = initialStock.ToString(),
        });
        return await factory.WithContextAsync(db => db.Products.Where(p => p.Name == name).Select(p => p.Id).SingleAsync());
    }

    private Task<int> StockOfAsync(int productId) =>
        factory.WithContextAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());

    [Fact]
    public async Task CreateProduct_RecordsInitialStockMovement()
    {
        var client = await CreateAdminClientAsync();

        var productId = await CreateProductAsync(client, initialStock: 15);

        var movement = await factory.WithContextAsync(db => db.StockMovements.Include(m => m.User).SingleAsync(m => m.ProductId == productId));
        Assert.Equal((StockMovementType.Initial, 15, 15), (movement.Type, movement.Quantity, movement.StockAfter));
        Assert.Equal("admin@supermarket.local", movement.User!.Email);
    }

    [Fact]
    public async Task EditProduct_CannotChangeStockDirectly()
    {
        var client = await CreateAdminClientAsync();
        var productId = await CreateProductAsync(client, initialStock: 5);
        var product = await factory.WithContextAsync(db => db.Products.AsNoTracking().SingleAsync(p => p.Id == productId));

        await client.SubmitFormAsync($"/Products/Edit/{productId}", new Dictionary<string, string>
        {
            ["Product.Name"] = product.Name,
            ["Product.CategoryId"] = product.CategoryId.ToString(),
            ["Product.Price"] = "1500",
            ["Product.Stock"] = "999",
        });

        Assert.Equal(5, await StockOfAsync(productId));
        Assert.Equal(1500m, await factory.WithContextAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.Price).SingleAsync()));
    }

    [Fact]
    public async Task Receive_UpdatesStockAndShowsMovementInHistory()
    {
        var client = await CreateAdminClientAsync();
        var productId = await CreateProductAsync(client, initialStock: 3);

        var response = await client.SubmitFormAsync($"/Inventory/Receive?productId={productId}", new Dictionary<string, string>
        {
            ["Input.ProductId"] = productId.ToString(),
            ["Input.Quantity"] = "12",
            ["Input.Note"] = "Proveedor Andino, factura 881",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(15, await StockOfAsync(productId));

        var history = await client.GetHtmlAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("Proveedor Andino, factura 881", history);
        Assert.Contains("+12", history);
    }

    [Fact]
    public async Task Adjust_WithoutReason_ShowsValidationError()
    {
        var client = await CreateAdminClientAsync();
        var productId = await CreateProductAsync(client, initialStock: 8);

        var response = await client.SubmitFormAsync("/Inventory/Adjust", new Dictionary<string, string>
        {
            ["Input.ProductId"] = productId.ToString(),
            ["Input.CountedStock"] = "6",
            ["Input.Reason"] = string.Empty,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Indica el motivo del ajuste.", await response.ReadHtmlAsync());
        Assert.Equal(8, await StockOfAsync(productId));
    }

    [Fact]
    public async Task Adjust_SetsCountedStock()
    {
        var client = await CreateAdminClientAsync();
        var productId = await CreateProductAsync(client, initialStock: 8);

        var response = await client.SubmitFormAsync("/Inventory/Adjust", new Dictionary<string, string>
        {
            ["Input.ProductId"] = productId.ToString(),
            ["Input.CountedStock"] = "6",
            ["Input.Reason"] = "Dos unidades vencidas",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(6, await StockOfAsync(productId));
    }
}
