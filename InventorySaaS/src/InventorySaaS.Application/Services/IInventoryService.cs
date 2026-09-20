using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;

namespace InventorySaaS.Application.Services;

public interface IInventoryService
{
    Task<PaginatedList<InventoryBalanceDto>> GetBalancesAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? productId,
        CancellationToken cancellationToken);

    Task<PaginatedList<InventoryTransactionDto>> GetTransactionsAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? productId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Receives stock. <paramref name="idempotencyKey"/> comes from the caller's
    /// <c>Idempotency-Key</c> header; replaying a key returns the original result instead of
    /// posting a second transaction, which is what makes a double-scan safe.
    /// </summary>
    Task<InventoryTransactionDto> StockInAsync(
        StockInRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null);

    Task<InventoryTransactionDto> StockOutAsync(
        StockOutRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null);

    Task<InventoryTransactionDto> TransferAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null);

    Task<InventoryTransactionDto> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken);
}
