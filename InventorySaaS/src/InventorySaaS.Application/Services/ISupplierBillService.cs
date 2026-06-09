using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;

namespace InventorySaaS.Application.Services;

public interface ISupplierBillService
{
    Task<PaginatedList<SupplierBillDto>> GetAllAsync(PaginationParams pagination, Guid? supplierId, string? status, CancellationToken cancellationToken);
    Task<SupplierBillDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SupplierBillDto> CreateAsync(CreateSupplierBillRequest request, CancellationToken cancellationToken);
    Task<SupplierBillDto> CreateFromPurchaseOrderAsync(CreateBillFromPurchaseOrderRequest request, CancellationToken cancellationToken);
    Task<SupplierBillDto> ApproveAsync(Guid id, CancellationToken cancellationToken);
    Task<SupplierBillDto> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<List<OutstandingBillDto>> GetOutstandingBySupplierAsync(Guid supplierId, CancellationToken cancellationToken);
}
