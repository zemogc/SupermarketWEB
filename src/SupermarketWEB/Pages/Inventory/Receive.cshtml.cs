using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Inventory;

public class ReceiveModel(SupermarketContext context, InventoryService inventory) : InventoryFormPageModel(context)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(int? productId)
    {
        Input.ProductId = productId;
        await LoadProductOptionsAsync(productId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadProductOptionsAsync(Input.ProductId);
            return Page();
        }

        var result = await inventory.ReceiveAsync(Input.ProductId!.Value, Input.Quantity, Input.Note, User.GetUserId());
        var message = result.Succeeded
            ? $"Entrada registrada: +{Input.Quantity} unidades. Stock actual: {result.Value!.StockAfter}."
            : string.Empty;

        return await HandleResultAsync(result, Input.ProductId.Value, message);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Selecciona un producto.")]
        [Display(Name = "Producto")]
        public int? ProductId { get; set; }

        [Range(1, InventoryService.MaxQuantity, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
        [Display(Name = "Cantidad recibida")]
        public int Quantity { get; set; } = 1;

        [StringLength(300, ErrorMessage = "La nota no puede superar los {1} caracteres.")]
        [Display(Name = "Nota (proveedor, factura…)")]
        public string? Note { get; set; }
    }
}
