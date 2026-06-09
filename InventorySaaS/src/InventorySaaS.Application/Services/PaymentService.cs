using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PaymentService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<PaymentDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        var query = _context.Payments
            .Include(p => p.Customer)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.Invoice)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(p =>
                p.PaymentNumber.ToLower().Contains(searchTerm) ||
                p.Customer.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "paymentnumber" => pagination.SortDescending ? query.OrderByDescending(p => p.PaymentNumber) : query.OrderBy(p => p.PaymentNumber),
            "customer" => pagination.SortDescending ? query.OrderByDescending(p => p.Customer.Name) : query.OrderBy(p => p.Customer.Name),
            "amount" => pagination.SortDescending ? query.OrderByDescending(p => p.Amount) : query.OrderBy(p => p.Amount),
            _ => query.OrderByDescending(p => p.PaymentDate)
        };

        var projected = query.Select(p => new PaymentDto(
            p.Id, p.PaymentNumber, p.CustomerId, p.Customer.Name,
            p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference, p.Notes,
            p.Allocations.Select(a => new PaymentAllocationDto(
                a.InvoiceId, a.Invoice.InvoiceNumber, a.Amount)).ToList()));

        return await PaginatedList<PaymentDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<PaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .Include(p => p.Customer)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.Invoice)
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), id);

        return ToDto(payment);
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerInfo), request.CustomerId);

        if (request.Amount <= 0)
            throw new BadRequestException("Payment amount must be greater than zero.");

        if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var method))
            throw new BadRequestException($"Unknown payment method '{request.Method}'.");

        var allocations = request.Allocations ?? [];
        if (allocations.Any(a => a.Amount <= 0))
            throw new BadRequestException("Allocation amounts must be greater than zero.");

        var totalAllocated = allocations.Sum(a => a.Amount);
        if (totalAllocated > request.Amount)
            throw new BadRequestException($"Allocated total ({totalAllocated}) exceeds the payment amount ({request.Amount}).");

        if (allocations.Select(a => a.InvoiceId).Distinct().Count() != allocations.Count)
            throw new BadRequestException("Each invoice may only be allocated once per payment.");

        var tenantId = _currentUserService.TenantId!.Value;
        var paymentDate = request.PaymentDate ?? DateTime.UtcNow;

        var payment = new Payment
        {
            TenantId = tenantId,
            PaymentNumber = await GeneratePaymentNumberAsync(cancellationToken),
            CustomerId = request.CustomerId,
            PaymentDate = paymentDate,
            Amount = request.Amount,
            Method = method,
            Reference = request.Reference,
            Notes = request.Notes
        };

        foreach (var alloc in allocations)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == alloc.InvoiceId && !i.IsDeleted, cancellationToken)
                ?? throw new BadRequestException($"Invoice {alloc.InvoiceId} was not found.");

            if (invoice.CustomerId != request.CustomerId)
                throw new BadRequestException($"Invoice '{invoice.InvoiceNumber}' belongs to a different customer.");

            if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
                throw new BadRequestException($"Invoice '{invoice.InvoiceNumber}' is not open for payment (status '{invoice.Status}').");

            if (alloc.Amount > invoice.BalanceDue)
                throw new BadRequestException($"Allocation of {alloc.Amount} exceeds the outstanding balance ({invoice.BalanceDue}) on invoice '{invoice.InvoiceNumber}'.");

            invoice.ApplyPayment(alloc.Amount);

            payment.Allocations.Add(new PaymentAllocation
            {
                TenantId = tenantId,
                InvoiceId = invoice.Id,
                Amount = alloc.Amount
            });
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(payment.Id, cancellationToken);
    }

    private async Task<string> GeneratePaymentNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.Payments
            .Where(p => p.PaymentNumber.StartsWith($"PAY-{today}"))
            .CountAsync(cancellationToken);
        return $"PAY-{today}-{(todayCount + 1):D4}";
    }

    private static PaymentDto ToDto(Payment p) => new(
        p.Id, p.PaymentNumber, p.CustomerId, p.Customer.Name,
        p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference, p.Notes,
        p.Allocations.Select(a => new PaymentAllocationDto(
            a.InvoiceId, a.Invoice.InvoiceNumber, a.Amount)).ToList());
}
