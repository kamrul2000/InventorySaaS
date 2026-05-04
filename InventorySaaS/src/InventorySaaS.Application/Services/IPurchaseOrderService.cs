using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;

namespace InventorySaaS.Application.Services;

public interface IPurchaseOrderService
{
    Task<PaginatedList<PurchaseOrderDto>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken);
    Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken);
    Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseOrderDto> ReceiveAsync(Guid id, ReceiveGoodsRequest request, CancellationToken cancellationToken);
}
