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

    public static async Task InitializeAsync(IServiceProvider services, bool seedDemoUser)
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
                CreatedAtUtc = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.HashPassword(user, DemoUserPassword);
            context.Users.Add(user);
        }

        await context.SaveChangesAsync();
    }

    private static void SeedCatalog(SupermarketContext context)
    {
        var dairy = new Category { Name = "Lácteos", Description = "Leche, quesos y derivados" };
        var bakery = new Category { Name = "Panadería", Description = "Pan y productos horneados" };
        var produce = new Category { Name = "Frutas y verduras" };
        var cleaning = new Category { Name = "Aseo", Description = "Productos de limpieza para el hogar" };

        context.Categories.AddRange(dairy, bakery, produce, cleaning);
        context.Products.AddRange(
            new Product { Name = "Leche entera 1 L", Price = 4500m, Stock = 40, Category = dairy },
            new Product { Name = "Queso campesino 500 g", Price = 12900m, Stock = 15, Category = dairy },
            new Product { Name = "Yogur natural", Price = 3200m, Stock = 4, Category = dairy },
            new Product { Name = "Pan tajado", Price = 6800m, Stock = 25, Category = bakery },
            new Product { Name = "Manzana roja (kg)", Price = 8900m, Stock = 30, Category = produce },
            new Product { Name = "Tomate (kg)", Price = 5400m, Stock = 0, Category = produce },
            new Product { Name = "Detergente líquido 2 L", Price = 21500m, Stock = 12, Category = cleaning });
    }
}
