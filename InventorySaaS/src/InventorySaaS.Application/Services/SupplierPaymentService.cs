using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SupplierPaymentService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<SupplierPaymentDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? supplierId,
        CancellationToken cancellationToken)
    {
        var query = _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.SupplierBill)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(p =>
                p.PaymentNumber.ToLower().Contains(searchTerm) ||
                p.Supplier.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "paymentnumber" => pagination.SortDescending ? query.OrderByDescending(p => p.PaymentNumber) : query.OrderBy(p => p.PaymentNumber),
            "supplier" => pagination.SortDescending ? query.OrderByDescending(p => p.Supplier.Name) : query.OrderBy(p => p.Supplier.Name),
            "amount" => pagination.SortDescending ? query.OrderByDescending(p => p.Amount) : query.OrderBy(p => p.Amount),
            _ => query.OrderByDescending(p => p.PaymentDate)
        };

        var projected = query.Select(p => new SupplierPaymentDto(
            p.Id, p.PaymentNumber, p.SupplierId, p.Supplier.Name,
            p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference, p.Notes,
            p.Allocations.Select(a => new SupplierPaymentAllocationDto(
                a.SupplierBillId, a.SupplierBill.BillNumber, a.Amount)).ToList()));

        return await PaginatedList<SupplierPaymentDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<SupplierPaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.SupplierBill)
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierPayment), id);

        return ToDto(payment);
    }

    public async Task<SupplierPaymentDto> CreateAsync(CreateSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierInfo), request.SupplierId);

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

        if (allocations.Select(a => a.BillId).Distinct().Count() != allocations.Count)
            throw new BadRequestException("Each bill may only be allocated once per payment.");

        var tenantId = _currentUserService.TenantId!.Value;
        var paymentDate = request.PaymentDate ?? DateTime.UtcNow;

        var payment = new SupplierPayment
        {
            TenantId = tenantId,
            PaymentNumber = await GeneratePaymentNumberAsync(cancellationToken),
            SupplierId = request.SupplierId,
            PaymentDate = paymentDate,
            Amount = request.Amount,
            Method = method,
            Reference = request.Reference,
            Notes = request.Notes
        };

        foreach (var alloc in allocations)
        {
            var bill = await _context.SupplierBills
                .FirstOrDefaultAsync(b => b.Id == alloc.BillId && !b.IsDeleted, cancellationToken)
                ?? throw new BadRequestException($"Bill {alloc.BillId} was not found.");

            if (bill.SupplierId != request.SupplierId)
                throw new BadRequestException($"Bill '{bill.BillNumber}' belongs to a different supplier.");

            if (bill.Status is BillStatus.Draft or BillStatus.Cancelled)
                throw new BadRequestException($"Bill '{bill.BillNumber}' is not open for payment (status '{bill.Status}').");

            if (alloc.Amount > bill.BalanceDue)
                throw new BadRequestException($"Allocation of {alloc.Amount} exceeds the outstanding balance ({bill.BalanceDue}) on bill '{bill.BillNumber}'.");

            bill.ApplyPayment(alloc.Amount);

            payment.Allocations.Add(new SupplierPaymentAllocation
            {
                TenantId = tenantId,
                SupplierBillId = bill.Id,
                Amount = alloc.Amount
            });
        }

        _context.SupplierPayments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(payment.Id, cancellationToken);
    }

    private async Task<string> GeneratePaymentNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.SupplierPayments
            .Where(p => p.PaymentNumber.StartsWith($"SPAY-{today}"))
            .CountAsync(cancellationToken);
        return $"SPAY-{today}-{(todayCount + 1):D4}";
    }

    private static SupplierPaymentDto ToDto(SupplierPayment p) => new(
        p.Id, p.PaymentNumber, p.SupplierId, p.Supplier.Name,
        p.PaymentDate, p.Amount, p.Method.ToString(), p.Reference, p.Notes,
        p.Allocations.Select(a => new SupplierPaymentAllocationDto(
            a.SupplierBillId, a.SupplierBill.BillNumber, a.Amount)).ToList());
}
