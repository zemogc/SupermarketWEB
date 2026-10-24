using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;
using SupermarketWEB.Services;

namespace SupermarketWEB.Data;

/// <summary>
/// Genera un historial de operación realista (compras, ventas y anulaciones de los últimos
/// días) para que el panel, los reportes y el kardex se puedan explorar desde el primer inicio.
/// </summary>
/// <remarks>
/// Todo pasa por <see cref="InventoryService"/> y <see cref="SaleService"/>, así que los datos
/// respetan las mismas reglas que la operación real: el stock nunca queda negativo y la suma de
/// movimientos coincide con el stock. Usa una semilla fija para que el resultado sea reproducible.
/// </remarks>
public static class DemoDataSeeder
{
    public const string DemoCashierEmail = "cajero@supermarket.local";
    public const string DemoCashierPassword = "Cajero123!";

    private const int Days = 30;

    public static async Task SeedAsync(SupermarketContext context, IPasswordHasher<User> passwordHasher, TimeProvider timeProvider)
    {
        if (await context.Sales.AnyAsync())
        {
            return;
        }

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
        if (admin is null)
        {
            return;
        }

        var random = new Random(20260315);
        var today = timeProvider.Today();
        var firstDay = today.AddDays(-(Days - 1));

        // El catálogo y el administrador se crearon "ahora"; la historia simulada empieza un mes
        // antes, así que la apertura se fecha antes del primer día para que el kardex sea cronológico.
        var openingUtc = timeProvider.StartOfDayUtc(firstDay.AddDays(-1)).AddHours(6);
        await context.StockMovements
            .Where(m => m.Type == StockMovementType.Initial)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAtUtc, openingUtc));
        admin.CreatedAtUtc = openingUtc;
        await context.SaveChangesAsync();

        var cashier = await EnsureCashierAsync(context, passwordHasher, openingUtc);
        await EnsureCustomersAsync(context);

        var supplierIds = await EnsureSuppliersAsync(context);
        var customerIds = await context.Customers.Select(c => c.Id).ToListAsync();
        var payModeIds = await context.PayModes.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();

        // Compra inicial de mercancía la víspera del primer día, salvo para los productos que se
        // dejan escasos a propósito para que el panel muestre alertas de stock.
        var restocked = await context.Products
            .Where(p => p.Name != "Tomate (kg)" && p.Name != "Yogur natural")
            .Select(p => new { p.Id, p.Price })
            .ToListAsync();

        // Cada producto se compra a un proveedor fijo con un costo entre el 62 % y el 78 % del precio.
        var purchaseTerms = restocked.ToDictionary(
            p => p.Id,
            p => (SupplierId: supplierIds[random.Next(supplierIds.Count)], Cost: Math.Round(p.Price * (decimal)(0.62 + random.NextDouble() * 0.16), 0)));

        var purchaseTime = At(timeProvider, firstDay.AddDays(-1), hour: 7);
        foreach (var product in restocked)
        {
            var (supplierId, cost) = purchaseTerms[product.Id];
            await new InventoryService(context, purchaseTime)
                .ReceiveAsync(product.Id, random.Next(90, 140), "Pedido inicial", admin.Id, supplierId, cost);
        }

        // Todo se registra en orden cronológico, para que el saldo de cada movimiento
        // corresponda al stock real en ese instante.
        var saleCount = 0;
        for (var day = firstDay; day <= today; day = day.AddDays(1))
        {
            // Los fines de semana se vende más. Minutos del día distintos y ordenados (8:00 a 20:59).
            var salesToday = random.Next(2, 6) + (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 3 : 0);
            var minutesOfDay = Enumerable.Range(0, salesToday).Select(_ => random.Next(8 * 60, 21 * 60)).Distinct().Order();

            foreach (var minuteOfDay in minutesOfDay)
            {
                var saleTime = At(timeProvider, day, minuteOfDay / 60, minuteOfDay % 60);
                if (saleTime.GetUtcNow() > timeProvider.GetUtcNow())
                {
                    break; // hoy solo hasta la hora actual
                }

                var available = await context.Products
                    .Where(p => p.Stock >= 3)
                    .Select(p => p.Id)
                    .ToListAsync();

                if (available.Count == 0)
                {
                    break;
                }

                var lines = available
                    .OrderBy(_ => random.Next())
                    .Take(random.Next(1, Math.Min(4, available.Count) + 1))
                    .Select(id => new SaleLineRequest(id, random.Next(1, 4)))
                    .ToList();

                var request = new RegisterSaleRequest(
                    random.Next(3) == 0 ? null : customerIds[random.Next(customerIds.Count)],
                    payModeIds[random.Next(payModeIds.Count)],
                    random.Next(3) == 0 ? admin.Id : cashier.Id,
                    lines);

                var result = await CreateSaleService(context, saleTime).RegisterAsync(request);

                // Algunas ventas se anulan enseguida (30 segundos después, antes de la siguiente venta).
                if (result.Succeeded && ++saleCount % 37 == 5)
                {
                    var cancelTime = new FixedTimeProvider(saleTime.GetUtcNow().UtcDateTime.AddSeconds(30), timeProvider.LocalTimeZone);
                    await CreateSaleService(context, cancelTime).CancelAsync(result.Value!.Id, admin.Id);
                }
            }

            // Reposición a mitad de periodo para que el kardex muestre más de una entrada.
            if (day == firstDay.AddDays(Days / 2))
            {
                var restockTime = At(timeProvider, day, hour: 21);
                foreach (var product in restocked.Where((_, index) => index % 2 == 0))
                {
                    // El proveedor subió un 4 % el precio: el costo promedio refleja la mezcla.
                    var (supplierId, cost) = purchaseTerms[product.Id];
                    await new InventoryService(context, restockTime)
                        .ReceiveAsync(product.Id, 40, "Reposición quincenal", admin.Id, supplierId, Math.Round(cost * 1.04m, 0));
                }
            }

            // Un ajuste por conteo al cierre de un día reciente.
            if (day == today.AddDays(-2))
            {
                var adjusted = await context.Products.AsNoTracking().Where(p => p.Stock > 10).OrderBy(p => p.Id).FirstOrDefaultAsync();
                if (adjusted is not null)
                {
                    await new InventoryService(context, At(timeProvider, day, hour: 21))
                        .AdjustAsync(adjusted.Id, adjusted.Stock - 2, "Dos unidades dañadas en bodega", admin.Id);
                }
            }
        }

        context.ChangeTracker.Clear();
    }

    private static SaleService CreateSaleService(SupermarketContext context, TimeProvider time) =>
        new(context, new InventoryService(context, time), time);

    private static FixedTimeProvider At(TimeProvider timeProvider, DateOnly day, int hour, int minute = 0) =>
        new(timeProvider.StartOfDayUtc(day).AddHours(hour).AddMinutes(minute), timeProvider.LocalTimeZone);

    private static async Task<User> EnsureCashierAsync(SupermarketContext context, IPasswordHasher<User> passwordHasher, DateTime createdAtUtc)
    {
        var cashier = await context.Users.FirstOrDefaultAsync(u => u.Email == DemoCashierEmail);
        if (cashier is not null)
        {
            return cashier;
        }

        cashier = new User
        {
            Email = DemoCashierEmail,
            FullName = "Camila Rojas",
            Role = UserRole.Cashier,
            CreatedAtUtc = createdAtUtc,
        };
        cashier.PasswordHash = passwordHasher.HashPassword(cashier, DemoCashierPassword);
        context.Users.Add(cashier);
        await context.SaveChangesAsync();
        return cashier;
    }

    private static async Task<List<int>> EnsureSuppliersAsync(SupermarketContext context)
    {
        Supplier[] suppliers =
        [
            new() { Name = "Distribuidora Andina", TaxId = "900123456-1", ContactName = "Jorge Pardo", Phone = "6015550100", Email = "ventas@andina.example.com" },
            new() { Name = "Lácteos del Valle", TaxId = "900654321-2", ContactName = "Marcela Ortiz", Phone = "6025550101" },
            new() { Name = "Comercializadora La Cosecha", TaxId = "901112223-3", ContactName = "Pedro Salas", Email = "pedidos@lacosecha.example.com" },
        ];

        var existing = await context.Suppliers.Select(s => s.Name).ToListAsync();
        context.Suppliers.AddRange(suppliers.Where(s => !existing.Contains(s.Name)));
        await context.SaveChangesAsync();

        return await context.Suppliers.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
    }

    private static async Task EnsureCustomersAsync(SupermarketContext context)
    {
        Customer[] customers =
        [
            new() { DocumentNumber = "1003", FirstName = "Sofía", LastName = "Hernández", Email = "sofia@example.com", Phone = "3105550101" },
            new() { DocumentNumber = "1004", FirstName = "Andrés", LastName = "Castillo", Phone = "3155550102" },
            new() { DocumentNumber = "1005", FirstName = "Valentina", LastName = "Ruiz", Email = "valentina@example.com" },
            new() { DocumentNumber = "1006", FirstName = "Mateo", LastName = "Vargas", Address = "Carrera 7 # 45-12" },
        ];

        var existing = await context.Customers.Select(c => c.DocumentNumber).ToListAsync();
        context.Customers.AddRange(customers.Where(c => !existing.Contains(c.DocumentNumber)));
        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTime utcNow, TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override TimeZoneInfo LocalTimeZone => localTimeZone;
    }
}
