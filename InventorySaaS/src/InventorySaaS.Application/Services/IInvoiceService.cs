using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;

namespace InventorySaaS.Application.Services;

public interface IInvoiceService
{
    Task<PaginatedList<InvoiceDto>> GetAllAsync(PaginationParams pagination, Guid? customerId, string? status, CancellationToken cancellationToken);
    Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken);
    Task<InvoiceDto> CreateFromSalesOrderAsync(CreateInvoiceFromSalesOrderRequest request, CancellationToken cancellationToken);
    Task<InvoiceDto> IssueAsync(Guid id, CancellationToken cancellationToken);
    Task<InvoiceDto> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<List<OutstandingInvoiceDto>> GetOutstandingByCustomerAsync(Guid customerId, CancellationToken cancellationToken);
}
