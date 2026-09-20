using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Inventory;

/// <summary>
/// A record that a scan-driven write with this key has already been applied.
///
/// Warehouse scanners double-fire and flaky Wi-Fi makes clients retry, so every inventory-changing
/// scan endpoint carries an <c>Idempotency-Key</c>. The unique index on (TenantId, Key) is what
/// actually makes a replay safe: the second insert loses the race and the caller is handed the
/// stored response instead of posting a second transaction.
/// </summary>
public class ScanIdempotencyKey : TenantEntity
{
    public string Key { get; set; } = default!;

    /// <summary>The route the key was first used against; replaying it elsewhere is rejected.</summary>
    public string Endpoint { get; set; } = default!;

    /// <summary>The serialised original response, replayed verbatim on a duplicate request.</summary>
    public string? ResponseJson { get; set; }
}
