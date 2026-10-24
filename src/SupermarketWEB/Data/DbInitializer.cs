using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Models;

namespace SupermarketWEB.Data;

/// <summary>
/// Aplica las migraciones pendientes y carga datos de ejemplo cuando la base está vacía.
/// </summary>
public static class DbInitializer
{
    public const string DemoUserEmail = "admin@supermarket.local";
    public const string DemoUserPassword = "Admin123!";

    public static async Task InitializeAsync(IServiceProvider services, bool seedDemoUser, bool seedDemoData = false)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SupermarketContext>();

        await context.Database.MigrateAsync();

        if (!await context.Categories.AnyAsync())
        {
            SeedCatalog(context);
        }

        if (!await context.PayModes.AnyAsync())
        {
            context.PayModes.AddRange(
                new PayMode { Name = "Efectivo", Observation = "Pago en caja" },
                new PayMode { Name = "Tarjeta débito" },
                new PayMode { Name = "Tarjeta crédito", Observation = "Aplica a todas las franquicias" },
                new PayMode { Name = "Transferencia" });
        }

        if (!await context.Customers.AnyAsync())
        {
            context.Customers.AddRange(
                new Customer { DocumentNumber = "1001", FirstName = "Ana", LastName = "Martínez", Email = "ana@example.com", Phone = "3001234567" },
                new Customer { DocumentNumber = "1002", FirstName = "Luis", LastName = "Gómez", Address = "Calle 10 # 5-20" });
        }

        if (seedDemoUser && !await context.Users.AnyAsync())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var user = new User
            {
                Email = DemoUserEmail,
                FullName = "Administrador",
                Role = UserRole.Admin,
                CreatedAtUtc = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.HashPassword(user, DemoUserPassword);
            context.Users.Add(user);
        }

        await context.SaveChangesAsync();

        if (seedDemoData)
        {
            await DemoDataSeeder.SeedAsync(
                context,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
                scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }
    }

    private static void SeedCatalog(SupermarketContext context)
    {
        var dairy = new Category { Name = "Lácteos", Description = "Leche, quesos y derivados" };
        var bakery = new Category { Name = "Panadería", Description = "Pan y productos horneados" };
        var produce = new Category { Name = "Frutas y verduras" };
        var cleaning = new Category { Name = "Aseo", Description = "Productos de limpieza para el hogar" };
        var pantry = new Category { Name = "Despensa", Description = "Granos, aceites y abarrotes" };
        var drinks = new Category { Name = "Bebidas", Description = "Café, jugos y gaseosas" };

        Product[] products =
        [
            new() { Name = "Leche entera 1 L", Price = 4500m, Stock = 40, Category = dairy },
            new() { Name = "Queso campesino 500 g", Price = 12900m, Stock = 15, Category = dairy },
            new() { Name = "Yogur natural", Price = 3200m, Stock = 4, Category = dairy },
            new() { Name = "Pan tajado", Price = 6800m, Stock = 25, Category = bakery },
            new() { Name = "Manzana roja (kg)", Price = 8900m, Stock = 30, Category = produce },
            new() { Name = "Tomate (kg)", Price = 5400m, Stock = 0, Category = produce },
            new() { Name = "Detergente líquido 2 L", Price = 21500m, Stock = 12, Category = cleaning },
            new() { Name = "Jabón de baño x3", Price = 9800m, Stock = 20, Category = cleaning },
            new() { Name = "Arroz 1 kg", Price = 4200m, Stock = 60, Category = pantry },
            new() { Name = "Aceite vegetal 1 L", Price = 11900m, Stock = 18, Category = pantry },
            new() { Name = "Huevos x12", Price = 13500m, Stock = 24, Category = pantry },
            new() { Name = "Pasta spaghetti 500 g", Price = 3900m, Stock = 35, Category = pantry },
            new() { Name = "Café molido 500 g", Price = 18900m, Stock = 16, Category = drinks },
            new() { Name = "Jugo de naranja 1 L", Price = 7500m, Stock = 22, Category = drinks },
            new() { Name = "Gaseosa 1.5 L", Price = 6200m, Stock = 30, Category = drinks },
        ];

        context.Categories.AddRange(dairy, bakery, produce, cleaning, pantry, drinks);
        context.Products.AddRange(products);

        // El kardex arranca con el saldo inicial de cada producto.
        context.StockMovements.AddRange(products.Where(p => p.Stock > 0).Select(p => new StockMovement
        {
            Product = p,
            Type = StockMovementType.Initial,
            Quantity = p.Stock,
            StockAfter = p.Stock,
            Note = "Stock inicial",
            CreatedAtUtc = DateTime.UtcNow,
        }));
    }
}
