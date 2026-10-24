using Microsoft.EntityFrameworkCore;
using SupermarketWEB.Data;
using SupermarketWEB.Models;

namespace SupermarketWEB.Services;

/// <summary>
/// Único punto por el que cambia el stock. Cada cambio deja un <see cref="StockMovement"/>.
/// </summary>
public class InventoryService(SupermarketContext context, TimeProvider timeProvider)
{
    public const int MaxQuantity = 100_000;

    public const decimal MaxUnitCost = 100_000_000m;

    /// <summary>
    /// Registra una entrada de mercancía. Si se informa el costo unitario, recalcula el costo
    /// promedio ponderado del producto con las unidades que ya había en inventario.
    /// </summary>
    public async Task<ServiceResult<StockMovement>> ReceiveAsync(
        int productId, int quantity, string? note, int userId, int? supplierId = null, decimal? unitCost = null)
    {
        if (quantity is < 1 or > MaxQuantity)
        {
            return ServiceResult<StockMovement>.Failure($"La cantidad debe estar entre 1 y {MaxQuantity:N0}.");
        }

        if (unitCost is <= 0 or > MaxUnitCost)
        {
            return ServiceResult<StockMovement>.Failure("El costo unitario debe ser mayor que cero.");
        }

        if (supplierId is not null && !await context.Suppliers.AnyAsync(s => s.Id == supplierId))
        {
            return ServiceResult<StockMovement>.Failure("El proveedor seleccionado no existe.");
        }

        var current = await context.Products
            .Where(p => p.Id == productId)
            .Select(p => new { p.Stock, p.AverageCost })
            .SingleOrDefaultAsync();

        if (current is null)
        {
            return ServiceResult<StockMovement>.Failure("El producto no existe.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        int stockAfter;
        if (unitCost is null)
        {
            var applied = await TryApplyDeltaAsync(productId, quantity);
            if (applied is null)
            {
                return ServiceResult<StockMovement>.Failure("El producto no existe.");
            }

            stockAfter = applied.Value;
        }
        else
        {
            var averageCost = WeightedAverageCost(current.Stock, current.AverageCost, quantity, unitCost.Value);
            stockAfter = current.Stock + quantity;

            // SQLite no opera decimales en SQL, así que el promedio se calcula en memoria y se escribe
            // solo si el stock no cambió desde la lectura (toda entrada o venta lo modifica).
            var updated = await context.Products
                .Where(p => p.Id == productId && p.Stock == current.Stock)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, stockAfter)
                    .SetProperty(p => p.AverageCost, averageCost));

            if (updated == 0)
            {
                return ServiceResult<StockMovement>.Failure("El stock cambió mientras se registraba la entrada. Intenta de nuevo.");
            }
        }

        var movement = CreateMovement(productId, StockMovementType.Receipt, quantity, stockAfter, userId, note);
        movement.SupplierId = supplierId;
        movement.UnitCost = unitCost;
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<StockMovement>.Success(movement);
    }

    /// <summary>
    /// Promedio ponderado entre las unidades existentes y las recibidas. Si no había costo previo
    /// (o no quedaban unidades), el nuevo costo es el de la compra.
    /// </summary>
    public static decimal WeightedAverageCost(int currentStock, decimal? currentCost, int receivedQuantity, decimal receivedCost)
    {
        if (currentCost is null || currentStock <= 0)
        {
            return receivedCost;
        }

        var total = (currentStock * currentCost.Value) + (receivedQuantity * receivedCost);
        return Math.Round(total / (currentStock + receivedQuantity), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Fija el stock al valor contado físicamente y registra la diferencia.
    /// </summary>
    public async Task<ServiceResult<StockMovement>> AdjustAsync(int productId, int countedStock, string reason, int userId)
    {
        if (countedStock is < 0 or > MaxQuantity)
        {
            return ServiceResult<StockMovement>.Failure($"El stock contado debe estar entre 0 y {MaxQuantity:N0}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<StockMovement>.Failure("Indica el motivo del ajuste.");
        }

        var current = await context.Products
            .Where(p => p.Id == productId)
            .Select(p => (int?)p.Stock)
            .SingleOrDefaultAsync();

        if (current is null)
        {
            return ServiceResult<StockMovement>.Failure("El producto no existe.");
        }

        if (current == countedStock)
        {
            return ServiceResult<StockMovement>.Failure("El stock contado coincide con el registrado; no hay nada que ajustar.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Concurrencia optimista: si se vendió algo mientras se contaba, no se pisa ese cambio.
        var updated = await context.Products
            .Where(p => p.Id == productId && p.Stock == current)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, countedStock));

        if (updated == 0)
        {
            return ServiceResult<StockMovement>.Failure("El stock cambió mientras se registraba el ajuste. Revisa el valor y vuelve a intentarlo.");
        }

        var movement = CreateMovement(
            productId, StockMovementType.Adjustment, countedStock - current.Value, countedStock, userId, reason.Trim());
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<StockMovement>.Success(movement);
    }

    /// <summary>
    /// Suma <paramref name="delta"/> al stock de forma atómica, sin permitir que quede negativo.
    /// Debe llamarse dentro de una transacción. Devuelve el stock resultante, o null si el
    /// producto no existe o no tiene unidades suficientes.
    /// </summary>
    internal async Task<int?> TryApplyDeltaAsync(int productId, int delta)
    {
        var updated = await context.Products
            .Where(p => p.Id == productId && p.Stock + delta >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + delta));

        if (updated == 0)
        {
            return null;
        }

        return await context.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
    }

    internal StockMovement CreateMovement(
        int productId, StockMovementType type, int quantity, int stockAfter, int? userId, string? note = null, Sale? sale = null)
    {
        return new StockMovement
        {
            ProductId = productId,
            Type = type,
            Quantity = quantity,
            StockAfter = stockAfter,
            UserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Sale = sale,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
    }
}
