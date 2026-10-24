using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<SupermarketContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SupermarketDB")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Configuration.GetValue<bool>("SeedDemoUser"));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

await app.RunAsync();

// Expuesto para las pruebas de integración con WebApplicationFactory.
public partial class Program;
