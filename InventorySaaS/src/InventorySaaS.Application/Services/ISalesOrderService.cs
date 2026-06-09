using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;

namespace InventorySaaS.Application.Services;

public interface ISalesOrderService
{
    Task<PaginatedList<SalesOrderDto>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken);
    Task<SalesOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken);
    Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken);
    Task<SalesOrderDto> DeliverAsync(Guid id, DeliverSalesOrderRequest request, CancellationToken cancellationToken);
    Task<SalesOrderDto> ReturnAsync(Guid id, ReturnSalesOrderRequest request, CancellationToken cancellationToken);
    Task<SalesOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}
