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

    public async Task<ServiceResult<StockMovement>> ReceiveAsync(int productId, int quantity, string? note, int userId)
    {
        if (quantity is < 1 or > MaxQuantity)
        {
            return ServiceResult<StockMovement>.Failure($"La cantidad debe estar entre 1 y {MaxQuantity:N0}.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();

        var stockAfter = await TryApplyDeltaAsync(productId, quantity);
        if (stockAfter is null)
        {
            return ServiceResult<StockMovement>.Failure("El producto no existe.");
        }

        var movement = CreateMovement(productId, StockMovementType.Receipt, quantity, stockAfter.Value, userId, note);
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ServiceResult<StockMovement>.Success(movement);
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
