using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class SupplierBillService : ISupplierBillService
{
    private const int DefaultPaymentTermDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SupplierBillService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<SupplierBillDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? supplierId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _context.SupplierBills
            .Include(b => b.Supplier)
            .Include(b => b.PurchaseOrder)
            .Include(b => b.Items)
            .Where(b => !b.IsDeleted)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(b => b.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BillStatus>(status, true, out var parsedStatus))
            query = query.Where(b => b.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(b =>
                b.BillNumber.ToLower().Contains(searchTerm) ||
                b.Supplier.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "billnumber" => pagination.SortDescending ? query.OrderByDescending(b => b.BillNumber) : query.OrderBy(b => b.BillNumber),
            "supplier" => pagination.SortDescending ? query.OrderByDescending(b => b.Supplier.Name) : query.OrderBy(b => b.Supplier.Name),
            "duedate" => pagination.SortDescending ? query.OrderByDescending(b => b.DueDate) : query.OrderBy(b => b.DueDate),
            "status" => pagination.SortDescending ? query.OrderByDescending(b => b.Status) : query.OrderBy(b => b.Status),
            "amount" => pagination.SortDescending ? query.OrderByDescending(b => b.TotalAmount) : query.OrderBy(b => b.TotalAmount),
            _ => query.OrderByDescending(b => b.BillDate)
        };

        var projected = query.Select(b => new SupplierBillDto(
            b.Id, b.BillNumber, b.SupplierId, b.Supplier.Name,
            b.PurchaseOrderId, b.PurchaseOrder != null ? b.PurchaseOrder.OrderNumber : null,
            b.SupplierInvoiceNumber, b.BillDate, b.DueDate, b.Status.ToString(),
            b.SubTotal, b.TaxAmount, b.DiscountAmount, b.TotalAmount, b.AmountPaid, b.TotalAmount - b.AmountPaid, b.Notes,
            b.Items.Select(it => new SupplierBillItemDto(
                it.Id, it.ProductId, it.Description, it.Quantity,
                it.UnitPrice, it.TaxRate, it.DiscountRate, it.LineTotal)).ToList()));

        return await PaginatedList<SupplierBillDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<SupplierBillDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await _context.SupplierBills
            .Include(b => b.Supplier)
            .Include(b => b.PurchaseOrder)
            .Include(b => b.Items)
            .Where(b => b.Id == id && !b.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierBill), id);

        return ToDto(bill);
    }

    public async Task<SupplierBillDto> CreateAsync(CreateSupplierBillRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierInfo), request.SupplierId);

        if (request.Items.Count == 0)
            throw new BadRequestException("At least one bill line is required.");

        var tenantId = _currentUserService.TenantId!.Value;
        var billDate = DateTime.UtcNow;

        var bill = new SupplierBill
        {
            TenantId = tenantId,
            BillNumber = await GenerateBillNumberAsync(cancellationToken),
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            BillDate = billDate,
            DueDate = request.DueDate ?? billDate.AddDays(DefaultPaymentTermDays),
            Status = BillStatus.Draft,
            Notes = request.Notes
        };

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new BadRequestException("Each bill line requires a description.");

            bill.Items.Add(BuildItem(
                tenantId, item.ProductId, item.Description,
                item.Quantity, item.UnitPrice, item.TaxRate, item.DiscountRate));
        }

        ApplyTotals(bill);

        _context.SupplierBills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(bill.Id, cancellationToken);
    }

    public async Task<SupplierBillDto> CreateFromPurchaseOrderAsync(
        CreateBillFromPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Items)
                .ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId && !p.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId);

        var alreadyBilled = await _context.SupplierBills
            .AnyAsync(b => b.PurchaseOrderId == po.Id && b.Status != BillStatus.Cancelled, cancellationToken);
        if (alreadyBilled)
            throw new ConflictException($"Purchase order '{po.OrderNumber}' has already been billed.");

        if (po.Items.Count == 0)
            throw new BadRequestException("Cannot bill a purchase order with no items.");

        var tenantId = _currentUserService.TenantId!.Value;
        var billDate = DateTime.UtcNow;

        var bill = new SupplierBill
        {
            TenantId = tenantId,
            BillNumber = await GenerateBillNumberAsync(cancellationToken),
            SupplierId = po.SupplierId,
            PurchaseOrderId = po.Id,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            BillDate = billDate,
            DueDate = request.DueDate ?? billDate.AddDays(DefaultPaymentTermDays),
            // Generated from an approved order, so it is immediately payable.
            Status = BillStatus.Open,
            Notes = $"Generated from purchase order {po.OrderNumber}"
        };

        foreach (var poItem in po.Items)
        {
            bill.Items.Add(BuildItem(
                tenantId, poItem.ProductId, poItem.Product?.Name ?? "Item",
                poItem.Quantity, poItem.UnitPrice, poItem.TaxRate, poItem.DiscountRate));
        }

        ApplyTotals(bill);

        _context.SupplierBills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(bill.Id, cancellationToken);
    }

    public async Task<SupplierBillDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await LoadFullAsync(id, cancellationToken);

        if (bill.Status != BillStatus.Draft)
            throw new BadRequestException($"Only draft bills can be approved (current status '{bill.Status}').");

        bill.Status = BillStatus.Open;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(bill);
    }

    public async Task<SupplierBillDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var bill = await LoadFullAsync(id, cancellationToken);

        if (bill.Status == BillStatus.Cancelled)
            throw new BadRequestException("Bill is already cancelled.");

        if (bill.AmountPaid > 0)
            throw new BadRequestException("Cannot cancel a bill that has payments applied. Void the payments first.");

        bill.Status = BillStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(bill);
    }

    public async Task<List<OutstandingBillDto>> GetOutstandingBySupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        return await _context.SupplierBills
            .Where(b => b.SupplierId == supplierId
                && !b.IsDeleted
                && (b.Status == BillStatus.Open
                    || b.Status == BillStatus.PartiallyPaid
                    || b.Status == BillStatus.Overdue)
                && b.TotalAmount > b.AmountPaid)
            .OrderBy(b => b.DueDate)
            .Select(b => new OutstandingBillDto(
                b.Id, b.BillNumber, b.BillDate, b.DueDate,
                b.TotalAmount, b.AmountPaid, b.TotalAmount - b.AmountPaid))
            .ToListAsync(cancellationToken);
    }

    private async Task<SupplierBill> LoadFullAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.SupplierBills
            .Include(b => b.Supplier)
            .Include(b => b.PurchaseOrder)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierBill), id);

    private async Task<string> GenerateBillNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.SupplierBills
            .Where(b => b.BillNumber.StartsWith($"BILL-{today}"))
            .CountAsync(cancellationToken);
        return $"BILL-{today}-{(todayCount + 1):D4}";
    }

    private static SupplierBillItem BuildItem(
        Guid tenantId, Guid? productId, string description,
        int quantity, decimal unitPrice, decimal taxRate, decimal discountRate)
    {
        var lineSubTotal = quantity * unitPrice;
        var lineTax = lineSubTotal * (taxRate / 100m);
        var lineDiscount = lineSubTotal * (discountRate / 100m);

        return new SupplierBillItem
        {
            TenantId = tenantId,
            ProductId = productId,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRate = taxRate,
            DiscountRate = discountRate,
            LineTotal = lineSubTotal + lineTax - lineDiscount
        };
    }

    private static void ApplyTotals(SupplierBill bill)
    {
        decimal subTotal = 0, tax = 0, discount = 0;
        foreach (var item in bill.Items)
        {
            var lineSubTotal = item.Quantity * item.UnitPrice;
            subTotal += lineSubTotal;
            tax += lineSubTotal * (item.TaxRate / 100m);
            discount += lineSubTotal * (item.DiscountRate / 100m);
        }

        bill.SubTotal = subTotal;
        bill.TaxAmount = tax;
        bill.DiscountAmount = discount;
        bill.TotalAmount = subTotal + tax - discount;
    }

    private static SupplierBillDto ToDto(SupplierBill b) => new(
        b.Id, b.BillNumber, b.SupplierId, b.Supplier.Name,
        b.PurchaseOrderId, b.PurchaseOrder != null ? b.PurchaseOrder.OrderNumber : null,
        b.SupplierInvoiceNumber, b.BillDate, b.DueDate, b.Status.ToString(),
        b.SubTotal, b.TaxAmount, b.DiscountAmount, b.TotalAmount, b.AmountPaid, b.BalanceDue, b.Notes,
        b.Items.Select(it => new SupplierBillItemDto(
            it.Id, it.ProductId, it.Description, it.Quantity,
            it.UnitPrice, it.TaxRate, it.DiscountRate, it.LineTotal)).ToList());
}
