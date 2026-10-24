using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Pages.Customers;

/// <summary>
/// Lógica común de los formularios de creación y edición de clientes.
/// </summary>
public abstract class CustomerFormPageModel(SupermarketContext context) : PageModel
{
    protected SupermarketContext Context { get; } = context;

    [BindProperty]
    public Customer Customer { get; set; } = new();

    protected async Task<bool> TryApplyFormAsync(Customer customer)
    {
        var bound = await TryUpdateModelAsync(
            customer,
            nameof(Customer),
            c => c.DocumentNumber,
            c => c.FirstName,
            c => c.LastName,
            c => c.Address,
            c => c.Birthday,
            c => c.Phone,
            c => c.Email);

        if (!bound)
        {
            return false;
        }

        customer.DocumentNumber = customer.DocumentNumber.Trim().ToUpperInvariant();
        customer.FirstName = customer.FirstName.Trim();
        customer.LastName = customer.LastName.Trim();
        customer.Email = customer.Email?.Trim().ToLowerInvariant();

        if (customer.Birthday > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError("Customer.Birthday", "La fecha de nacimiento no puede estar en el futuro.");
            return false;
        }

        var duplicated = await Context.Customers
            .AnyAsync(c => c.Id != customer.Id && c.DocumentNumber == customer.DocumentNumber);

        if (duplicated)
        {
            ModelState.AddModelError("Customer.DocumentNumber", "Ya existe un cliente con este documento.");
            return false;
        }

        return true;
    }
}
