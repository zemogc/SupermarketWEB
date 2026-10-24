using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Sales;

public class CreateModel(SupermarketContext context, SaleService saleService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<ProductOption> Products { get; private set; } = [];

    public SelectList CustomerOptions { get; private set; } = null!;

    public SelectList PayModeOptions { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Input.Lines = [new LineInput()];
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync();
        }

        // Las filas que el usuario dejó sin producto no forman parte de la venta. No se eliminan
        // de Input.Lines para que, si hay errores, cada fila conserve sus mensajes de validación.
        var lines = Input.Lines
            .Where(l => l.ProductId is not null)
            .Select(l => new SaleLineRequest(l.ProductId!.Value, l.Quantity))
            .ToList();

        var request = new RegisterSaleRequest(Input.CustomerId, Input.PayModeId!.Value, User.GetUserId(), lines);

        var result = await saleService.RegisterAsync(request);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return await RedisplayAsync();
        }

        TempData["StatusMessage"] = $"Venta N.º {DisplayFormat.SaleNumber(result.Value!.Id)} registrada por {DisplayFormat.Currency(result.Value.Total)}.";
        return RedirectToPage("./Details", new { id = result.Value.Id });
    }

    private async Task<IActionResult> RedisplayAsync()
    {
        if (Input.Lines.Count == 0)
        {
            Input.Lines = [new LineInput()];
        }

        await LoadOptionsAsync();
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        Products = await context.Products
            .AsNoTracking()
            .Where(p => p.Stock > 0)
            .OrderBy(p => p.Name)
            .Select(p => new ProductOption(p.Id, p.Name, p.Price, p.Stock))
            .ToListAsync();

        var customers = await context.Customers
            .AsNoTracking()
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Select(c => new { c.Id, Label = c.FirstName + " " + c.LastName + " (" + c.DocumentNumber + ")" })
            .ToListAsync();
        CustomerOptions = new SelectList(customers, "Id", "Label", Input.CustomerId);

        var payModes = await context.PayModes.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        PayModeOptions = new SelectList(payModes, "Id", "Name", Input.PayModeId);
    }

    public record ProductOption(int Id, string Name, decimal Price, int Stock);

    public class InputModel
    {
        [Display(Name = "Cliente")]
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Selecciona un medio de pago.")]
        [Display(Name = "Medio de pago")]
        public int? PayModeId { get; set; }

        public List<LineInput> Lines { get; set; } = [];
    }

    public class LineInput
    {
        [Display(Name = "Producto")]
        public int? ProductId { get; set; }

        [Range(1, 10_000, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
        [Display(Name = "Cantidad")]
        public int Quantity { get; set; } = 1;
    }
}
