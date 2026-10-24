using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages;

public class IndexModel(SupermarketContext context, TimeProvider timeProvider) : PageModel
{
    public int TodaySalesCount { get; private set; }

    public decimal TodaySalesTotal { get; private set; }

    public IReadOnlyList<Sale> RecentSales { get; private set; } = [];

    public int CategoryCount { get; private set; }

    public int ProductCount { get; private set; }

    public int CustomerCount { get; private set; }

    public int OutOfStockCount { get; private set; }

    public decimal InventoryValue { get; private set; }

    public IReadOnlyList<Product> LowStockProducts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        CategoryCount = await context.Categories.CountAsync();
        ProductCount = await context.Products.CountAsync();
        CustomerCount = await context.Customers.CountAsync();
        OutOfStockCount = await context.Products.CountAsync(p => p.Stock == 0);

        // SQLite no agrega columnas decimal en el servidor, así que se suma en memoria
        // trayendo solo las dos columnas necesarias.
        var stock = await context.Products.Select(p => new { p.Price, p.Stock }).ToListAsync();
        InventoryValue = stock.Sum(p => p.Price * p.Stock);

        LowStockProducts = await context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Stock <= Product.LowStockThreshold)
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Name)
            .Take(5)
            .ToListAsync();

        var startOfTodayUtc = timeProvider.StartOfDayUtc(timeProvider.Today());
        var todayTotals = await context.Sales
            .Where(s => s.Status == SaleStatus.Completed && s.CreatedAtUtc >= startOfTodayUtc)
            .Select(s => s.Total)
            .ToListAsync();
        TodaySalesCount = todayTotals.Count;
        TodaySalesTotal = todayTotals.Sum();

        RecentSales = await context.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ThenByDescending(s => s.Id)
            .Take(5)
            .ToListAsync();
    }

    public DateTime ToLocal(DateTime utc) => timeProvider.ToLocal(utc);
}
