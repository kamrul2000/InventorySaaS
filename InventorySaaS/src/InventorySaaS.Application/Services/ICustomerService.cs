using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Customers.DTOs;

namespace InventorySaaS.Application.Services;

public interface ICustomerService
{
    Task<PaginatedList<CustomerDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<CustomerDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);

    Task<CustomerDto> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken);
}
