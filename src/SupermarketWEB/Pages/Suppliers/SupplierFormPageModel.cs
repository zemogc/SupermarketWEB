using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Infrastructure;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Suppliers;

/// <summary>
/// Lógica común de los formularios de creación y edición de proveedores.
/// </summary>
public abstract class SupplierFormPageModel(SupermarketContext context) : PageModel
{
    protected SupermarketContext Context { get; } = context;

    [BindProperty]
    public Supplier Supplier { get; set; } = new();

    protected async Task<bool> TryApplyFormAsync(Supplier supplier)
    {
        var bound = await TryUpdateModelAsync(
            supplier,
            nameof(Supplier),
            s => s.Name,
            s => s.TaxId,
            s => s.ContactName,
            s => s.Phone,
            s => s.Email);

        if (!bound)
        {
            return false;
        }

        supplier.Name = supplier.Name.Trim();
        supplier.TaxId = string.IsNullOrWhiteSpace(supplier.TaxId) ? null : supplier.TaxId.Trim();
        supplier.Email = supplier.Email?.Trim().ToLowerInvariant();

        var others = Context.Suppliers.Where(s => s.Id != supplier.Id);

        if (await others.Select(s => s.Name).ContainsIgnoringCaseAsync(supplier.Name))
        {
            ModelState.AddModelError("Supplier.Name", "Ya existe un proveedor con este nombre.");
            return false;
        }

        if (supplier.TaxId is not null && await others.AnyAsync(s => s.TaxId == supplier.TaxId))
        {
            ModelState.AddModelError("Supplier.TaxId", "Ya existe un proveedor con este NIT.");
            return false;
        }

        return true;
    }
}
