namespace SupermarketWEB.Models;

public enum SaleStatus
{
    Completed = 0,
    Cancelled = 1,
}

public class Sale
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Cliente opcional: una venta sin cliente corresponde a "consumidor final".
    /// </summary>
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public int PayModeId { get; set; }

    public PayMode? PayMode { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public decimal Total { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];
}
