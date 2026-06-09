using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;

namespace InventorySaaS.Application.Services;

public interface ISupplierPaymentService
{
    Task<PaginatedList<SupplierPaymentDto>> GetAllAsync(PaginationParams pagination, Guid? supplierId, CancellationToken cancellationToken);
    Task<SupplierPaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SupplierPaymentDto> CreateAsync(CreateSupplierPaymentRequest request, CancellationToken cancellationToken);
}
