using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Sales;

/// <summary>
/// A warehouse picking run against a confirmed sales order.
///
/// Picking records intent only — it does not move stock. Quantities accumulate on
/// <see cref="SalesOrderItem.PickedQuantity"/>, and completing the session hands off to the
/// existing delivery path, which is what actually draws inventory down. Keeping picking off
/// <see cref="SalesOrderStatus"/> means no stored enum value changes meaning.
/// </summary>
public class SalesOrderPickSession : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public PickSessionStatus Status { get; set; } = PickSessionStatus.Open;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? PickedBy { get; set; }
    public string? Notes { get; set; }

    public SalesOrder SalesOrder { get; set; } = default!;
    public ICollection<SalesOrderPickEvent> Events { get; set; } = [];
}

/// <summary>
/// One scan during picking. Kept as its own row so the picking trail survives independently of
/// the aggregated <see cref="SalesOrderItem.PickedQuantity"/>.
/// </summary>
public class SalesOrderPickEvent : TenantEntity
{
    public Guid SessionId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public SalesOrderPickSession Session { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
}
