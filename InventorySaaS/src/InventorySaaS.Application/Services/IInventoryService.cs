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

    Task<InventoryTransactionDto> StockInAsync(StockInRequest request, CancellationToken cancellationToken);
    Task<InventoryTransactionDto> StockOutAsync(StockOutRequest request, CancellationToken cancellationToken);
    Task<InventoryTransactionDto> TransferAsync(StockTransferRequest request, CancellationToken cancellationToken);
    Task<InventoryTransactionDto> AdjustAsync(StockAdjustmentRequest request, CancellationToken cancellationToken);
}
