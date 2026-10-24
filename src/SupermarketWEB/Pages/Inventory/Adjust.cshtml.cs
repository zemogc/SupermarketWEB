using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Inventory;

public class AdjustModel(SupermarketContext context, InventoryService inventory) : InventoryFormPageModel(context)
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

        var result = await inventory.AdjustAsync(Input.ProductId!.Value, Input.CountedStock!.Value, Input.Reason, User.GetUserId());
        var message = result.Succeeded
            ? $"Ajuste registrado ({(result.Value!.Quantity > 0 ? "+" : null)}{result.Value.Quantity}). Stock actual: {result.Value.StockAfter}."
            : string.Empty;

        return await HandleResultAsync(result, Input.ProductId.Value, message);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Selecciona un producto.")]
        [Display(Name = "Producto")]
        public int? ProductId { get; set; }

        [Required(ErrorMessage = "Indica el stock contado.")]
        [Range(0, InventoryService.MaxQuantity, ErrorMessage = "El stock contado debe estar entre {1} y {2}.")]
        [Display(Name = "Stock contado físicamente")]
        public int? CountedStock { get; set; }

        [Required(ErrorMessage = "Indica el motivo del ajuste.")]
        [StringLength(300, ErrorMessage = "El motivo no puede superar los {1} caracteres.")]
        [Display(Name = "Motivo")]
        public string Reason { get; set; } = string.Empty;
    }
}
