using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Services;

public record SaleLineRequest(int ProductId, int Quantity);

public record RegisterSaleRequest(int? CustomerId, int PayModeId, int UserId, IReadOnlyList<SaleLineRequest> Lines);

/// <summary>
/// Registra y anula ventas manteniendo el inventario consistente.
/// </summary>
public class SaleService(SupermarketContext context, InventoryService inventory, TimeProvider timeProvider)
{
    public async Task<ServiceResult<Sale>> RegisterAsync(RegisterSaleRequest request)
    {
        if (request.Lines.Any(l => l.Quantity <= 0))
        {
            return ServiceResult<Sale>.Failure("Las cantidades deben ser mayores que cero.");
        }

        // Si un producto aparece en varias líneas se agrupa en una sola.
        var lines = request.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => new SaleLineRequest(g.Key, g.Sum(l => l.Quantity)))
            .ToList();

        if (lines.Count == 0)
        {
            return ServiceResult<Sale>.Failure("Agrega al menos un producto a la venta.");
        }

        var errors = new List<string>();

        if (!await context.PayModes.AnyAsync(p => p.Id == request.PayModeId))
        {
            errors.Add("El medio de pago seleccionado no existe.");
        }

        if (request.CustomerId is not null && !await context.Customers.AnyAsync(c => c.Id == request.CustomerId))
        {
            errors.Add("El cliente seleccionado no existe.");
        }

        var productIds = lines.Select(l => l.ProductId).ToList();
        var products = await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                errors.Add("Uno de los productos seleccionados ya no existe.");
            }
            else if (product.Stock < line.Quantity)
            {
                errors.Add($"Stock insuficiente para \"{product.Name}\": disponible {product.Stock}, solicitado {line.Quantity}.");
            }
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Sale>.Failure(errors);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        var stockAfter = new Dictionary<int, int>();

        foreach (var line in lines)
        {
            // Descuento condicional y atómico: si otra venta consumió el stock entre la
            // validación y este punto, no se actualiza ninguna fila y la venta se aborta.
            var remaining = await inventory.TryApplyDeltaAsync(line.ProductId, -line.Quantity);

            if (remaining is null)
            {
                await transaction.RollbackAsync();
                return ServiceResult<Sale>.Failure(
                    $"El stock de \"{products[line.ProductId].Name}\" cambió mientras se registraba la venta. Intenta de nuevo.");
            }

            stockAfter[line.ProductId] = remaining.Value;
        }

        var sale = new Sale
        {
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            CustomerId = request.CustomerId,
            PayModeId = request.PayModeId,
            UserId = request.UserId,
            Items = lines.Select(l => new SaleItem
            {
                ProductId = l.ProductId,
                ProductName = products[l.ProductId].Name,
                UnitPrice = products[l.ProductId].Price,
                Quantity = l.Quantity,
            }).ToList(),
        };
        sale.Total = sale.Items.Sum(i => i.Subtotal);

        context.Sales.Add(sale);
        context.StockMovements.AddRange(lines.Select(l => inventory.CreateMovement(
            l.ProductId, StockMovementType.Sale, -l.Quantity, stockAfter[l.ProductId], request.UserId, sale: sale)));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<Sale>.Success(sale);
    }

    public async Task<ServiceResult<Sale>> CancelAsync(int saleId, int userId)
    {
        var sale = await context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId);
        if (sale is null)
        {
            return ServiceResult<Sale>.Failure("La venta no existe.");
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            return ServiceResult<Sale>.Failure("La venta ya estaba anulada.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        foreach (var item in sale.Items)
        {
            // Sumar nunca deja el stock negativo, así que el resultado solo es nulo si el
            // producto desapareció, cosa que impide la restricción de clave foránea.
            var stockAfter = await inventory.TryApplyDeltaAsync(item.ProductId, item.Quantity)
                ?? throw new InvalidOperationException($"No se encontró el producto {item.ProductId} de la venta {saleId}.");

            context.StockMovements.Add(inventory.CreateMovement(
                item.ProductId, StockMovementType.SaleCancellation, item.Quantity, stockAfter, userId, sale: sale));
        }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<Sale>.Success(sale);
    }
}
