using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;

namespace InventorySaaS.Application.Services;

public interface IPaymentService
{
    Task<PaginatedList<PaymentDto>> GetAllAsync(PaginationParams pagination, Guid? customerId, CancellationToken cancellationToken);
    Task<PaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);
}
