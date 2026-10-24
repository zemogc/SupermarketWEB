using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupermarketWEB.Data;
using SupermarketWEB.Models;
using SupermarketWEB.Services;

namespace SupermarketWEB.Tests;

public sealed class SaleServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 14, 30, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SupermarketContext _context;
    private readonly SaleService _service;
    private readonly Product _milk;
    private readonly Product _bread;
    private readonly PayMode _cash;
    private readonly User _cashier;

    public SaleServiceTests()
    {
        _connection.Open();
        _context = CreateContext();
        _context.Database.EnsureCreated();

        var category = new Category { Name = "General" };
        _milk = new Product { Name = "Leche", Price = 4500m, Stock = 10, Category = category };
        _bread = new Product { Name = "Pan", Price = 2500.50m, Stock = 3, Category = category };
        _cash = new PayMode { Name = "Efectivo" };
        _cashier = new User { Email = "caja@example.com", FullName = "Caja", PasswordHash = "x" };
        _context.AddRange(_milk, _bread, _cash, _cashier);
        _context.SaveChanges();

        _service = CreateService(_context);
    }

    private static SaleService CreateService(SupermarketContext context)
    {
        var time = new FixedTimeProvider(Now);
        return new SaleService(context, new InventoryService(context, time), time);
    }

    private SupermarketContext CreateContext() =>
        new(new DbContextOptionsBuilder<SupermarketContext>().UseSqlite(_connection).Options);

    private RegisterSaleRequest Request(params SaleLineRequest[] lines) =>
        new(CustomerId: null, _cash.Id, _cashier.Id, lines);

    private int StockOf(Product product)
    {
        using var context = CreateContext();
        return context.Products.Single(p => p.Id == product.Id).Stock;
    }

    [Fact]
    public async Task Register_PersistsSaleWithSnapshotPricesAndDecrementsStock()
    {
        var result = await _service.RegisterAsync(Request(
            new SaleLineRequest(_milk.Id, 2),
            new SaleLineRequest(_bread.Id, 1)));

        Assert.True(result.Succeeded);
        var sale = result.Value!;
        Assert.Equal(2 * 4500m + 2500.50m, sale.Total);
        Assert.Equal(Now.UtcDateTime, sale.CreatedAtUtc);
        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Contains(sale.Items, i => i.ProductName == "Pan" && i.UnitPrice == 2500.50m);
        Assert.Equal(8, StockOf(_milk));
        Assert.Equal(2, StockOf(_bread));

        var movements = await _context.StockMovements.Where(m => m.SaleId == sale.Id).ToListAsync();
        Assert.All(movements, m => Assert.Equal((StockMovementType.Sale, _cashier.Id), (m.Type, m.UserId!.Value)));
        Assert.Contains(movements, m => m.ProductId == _milk.Id && m.Quantity == -2 && m.StockAfter == 8);
        Assert.Contains(movements, m => m.ProductId == _bread.Id && m.Quantity == -1 && m.StockAfter == 2);
    }

    [Fact]
    public async Task Register_MergesRepeatedProductsIntoOneLine()
    {
        var result = await _service.RegisterAsync(Request(
            new SaleLineRequest(_milk.Id, 1),
            new SaleLineRequest(_milk.Id, 3)));

        Assert.True(result.Succeeded);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(6, StockOf(_milk));
    }

    [Fact]
    public async Task Register_WithInsufficientStock_FailsWithoutChangingAnything()
    {
        var result = await _service.RegisterAsync(Request(
            new SaleLineRequest(_milk.Id, 2),
            new SaleLineRequest(_bread.Id, 4)));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Stock insuficiente para \"Pan\""));
        Assert.Equal(10, StockOf(_milk));
        Assert.Equal(3, StockOf(_bread));
        Assert.Equal(0, await _context.Sales.CountAsync());
    }

    [Fact]
    public async Task Register_WhenStockChangesConcurrently_RollsBackEarlierLines()
    {
        // Otra caja vende todo el pan después de la validación, justo antes del primer descuento.
        using var racingContext = new SupermarketContext(new DbContextOptionsBuilder<SupermarketContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new StockThiefInterceptor(_bread.Id))
            .Options);
        var racingService = CreateService(racingContext);

        var result = await racingService.RegisterAsync(Request(
            new SaleLineRequest(_milk.Id, 2),
            new SaleLineRequest(_bread.Id, 2)));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("cambió mientras se registraba la venta"));
        Assert.Equal(10, StockOf(_milk));
        Assert.Equal(3, StockOf(_bread)); // el robo simulado ocurrió dentro de la transacción revertida
        Assert.Equal(0, await _context.Sales.CountAsync());
        Assert.Equal(0, await _context.StockMovements.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Register_WithNonPositiveQuantity_Fails(int quantity)
    {
        var result = await _service.RegisterAsync(Request(new SaleLineRequest(_milk.Id, quantity)));

        Assert.False(result.Succeeded);
        Assert.Equal(10, StockOf(_milk));
    }

    [Fact]
    public async Task Register_WithoutLines_Fails()
    {
        var result = await _service.RegisterAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Contains("Agrega al menos un producto a la venta.", result.Errors);
    }

    [Fact]
    public async Task Register_WithUnknownReferences_ReportsEachError()
    {
        var result = await _service.RegisterAsync(new RegisterSaleRequest(
            CustomerId: 999, PayModeId: 999, _cashier.Id, [new SaleLineRequest(999, 1)]));

        Assert.False(result.Succeeded);
        Assert.Contains("El medio de pago seleccionado no existe.", result.Errors);
        Assert.Contains("El cliente seleccionado no existe.", result.Errors);
        Assert.Contains("Uno de los productos seleccionados ya no existe.", result.Errors);
    }

    [Fact]
    public async Task Cancel_RestoresStockAndMarksSaleAsCancelled()
    {
        var sale = (await _service.RegisterAsync(Request(new SaleLineRequest(_milk.Id, 4)))).Value!;

        var result = await _service.CancelAsync(sale.Id, _cashier.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(SaleStatus.Cancelled, result.Value!.Status);
        Assert.Equal(Now.UtcDateTime, result.Value.CancelledAtUtc);
        Assert.Equal(10, StockOf(_milk));

        var reversal = await _context.StockMovements.SingleAsync(m => m.SaleId == sale.Id && m.Type == StockMovementType.SaleCancellation);
        Assert.Equal((4, 10), (reversal.Quantity, reversal.StockAfter));
    }

    [Fact]
    public async Task Cancel_Twice_FailsAndDoesNotRestoreStockAgain()
    {
        var sale = (await _service.RegisterAsync(Request(new SaleLineRequest(_milk.Id, 4)))).Value!;
        await _service.CancelAsync(sale.Id, _cashier.Id);

        var result = await _service.CancelAsync(sale.Id, _cashier.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("La venta ya estaba anulada.", result.Errors);
        Assert.Equal(10, StockOf(_milk));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Antes del primer UPDATE de stock deja en cero el stock de otro producto,
    /// simulando una venta concurrente desde otra caja.
    /// </summary>
    private sealed class StockThiefInterceptor(int productId) : DbCommandInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_done && command.CommandText.Contains("UPDATE \"Products\""))
            {
                _done = true;
                await using var steal = command.Connection!.CreateCommand();
                steal.Transaction = command.Transaction;
                steal.CommandText = $"UPDATE \"Products\" SET \"Stock\" = 0 WHERE \"Id\" = {productId}";
                await steal.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
