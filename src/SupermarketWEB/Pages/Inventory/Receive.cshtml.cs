using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Services;

namespace SupermarketWEB.Pages.Inventory;

public class ReceiveModel(SupermarketContext context, InventoryService inventory) : InventoryFormPageModel(context)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public SelectList SupplierOptions { get; private set; } = null!;

    public async Task OnGetAsync(int? productId)
    {
        Input.ProductId = productId;
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var result = await inventory.ReceiveAsync(
            Input.ProductId!.Value, Input.Quantity, Input.Note, User.GetUserId(), Input.SupplierId, Input.UnitCost);

        if (!result.Succeeded)
        {
            await LoadSupplierOptionsAsync();
        }

        var message = result.Succeeded
            ? $"Entrada registrada: +{Input.Quantity} unidades. Stock actual: {result.Value!.StockAfter}."
            : string.Empty;

        return await HandleResultAsync(result, Input.ProductId.Value, message);
    }

    private async Task LoadOptionsAsync()
    {
        await LoadProductOptionsAsync(Input.ProductId);
        await LoadSupplierOptionsAsync();
    }

    private async Task LoadSupplierOptionsAsync()
    {
        var suppliers = await Context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        SupplierOptions = new SelectList(suppliers, nameof(Models.Supplier.Id), nameof(Models.Supplier.Name), Input.SupplierId);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Selecciona un producto.")]
        [Display(Name = "Producto")]
        public int? ProductId { get; set; }

        [Range(1, InventoryService.MaxQuantity, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
        [Display(Name = "Cantidad recibida")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "Proveedor")]
        public int? SupplierId { get; set; }

        [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
            ErrorMessage = "El costo unitario debe estar entre {1} y {2}.")]
        [Display(Name = "Costo unitario de compra")]
        public decimal? UnitCost { get; set; }

        [StringLength(300, ErrorMessage = "La nota no puede superar los {1} caracteres.")]
        [Display(Name = "Nota (proveedor, factura…)")]
        public string? Note { get; set; }
    }
}
