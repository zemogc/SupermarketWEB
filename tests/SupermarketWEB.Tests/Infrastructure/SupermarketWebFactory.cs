using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SupermarketWEB.Data;

namespace SupermarketWEB.Tests.Infrastructure;

/// <summary>
/// Levanta la aplicación completa sobre una base SQLite en memoria, aislada por instancia.
/// </summary>
public class SupermarketWebFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source=tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    // Una base en memoria compartida vive mientras exista al menos una conexión abierta.
    private readonly SqliteConnection _keepAliveConnection;

    public SupermarketWebFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    /// <summary>
    /// Si es falso, la base arranca sin usuarios (solo con el catálogo de ejemplo).
    /// </summary>
    protected virtual bool SeedDemoUser => true;

    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<T> WithContextAsync<T>(Func<SupermarketContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SupermarketContext>();
        return await action(context);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SupermarketDB", _connectionString);
        builder.UseSetting("SeedDemoUser", SeedDemoUser.ToString());
        builder.UseSetting("RateLimiting:AuthenticationPermitsPerMinute", "1000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}

public sealed class EmptyUsersWebFactory : SupermarketWebFactory
{
    protected override bool SeedDemoUser => false;
}
