using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class InvoiceService : IInvoiceService
{
    private const int DefaultPaymentTermDays = 30;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InvoiceService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<InvoiceDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? customerId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Items)
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(searchTerm) ||
                i.Customer.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "invoicenumber" => pagination.SortDescending ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber),
            "customer" => pagination.SortDescending ? query.OrderByDescending(i => i.Customer.Name) : query.OrderBy(i => i.Customer.Name),
            "duedate" => pagination.SortDescending ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            "status" => pagination.SortDescending ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            "amount" => pagination.SortDescending ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            _ => query.OrderByDescending(i => i.InvoiceDate)
        };

        var projected = query.Select(i => new InvoiceDto(
            i.Id, i.InvoiceNumber, i.CustomerId, i.Customer.Name,
            i.SalesOrderId, i.SalesOrder != null ? i.SalesOrder.OrderNumber : null,
            i.InvoiceDate, i.DueDate, i.Status.ToString(),
            i.SubTotal, i.TaxAmount, i.DiscountAmount, i.TotalAmount, i.AmountPaid, i.TotalAmount - i.AmountPaid, i.Notes,
            i.Items.Select(it => new InvoiceItemDto(
                it.Id, it.ProductId, it.Description, it.Quantity,
                it.UnitPrice, it.TaxRate, it.DiscountRate, it.LineTotal)).ToList()));

        return await PaginatedList<InvoiceDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<InvoiceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Items)
            .Where(i => i.Id == id && !i.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerInfo), request.CustomerId);

        if (request.Items.Count == 0)
            throw new BadRequestException("At least one invoice line is required.");

        var tenantId = _currentUserService.TenantId!.Value;
        var invoiceDate = DateTime.UtcNow;

        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken),
            CustomerId = request.CustomerId,
            InvoiceDate = invoiceDate,
            DueDate = request.DueDate ?? invoiceDate.AddDays(DefaultPaymentTermDays),
            Status = InvoiceStatus.Draft,
            Notes = request.Notes
        };

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new BadRequestException("Each invoice line requires a description.");

            invoice.Items.Add(BuildItem(
                tenantId, item.ProductId, item.Description,
                item.Quantity, item.UnitPrice, item.TaxRate, item.DiscountRate));
        }

        ApplyTotals(invoice);

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceDto> CreateFromSalesOrderAsync(
        CreateInvoiceFromSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Items)
                .ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.SalesOrderId);

        var alreadyInvoiced = await _context.Invoices
            .AnyAsync(i => i.SalesOrderId == so.Id && i.Status != InvoiceStatus.Cancelled, cancellationToken);
        if (alreadyInvoiced)
            throw new ConflictException($"Sales order '{so.OrderNumber}' has already been invoiced.");

        if (so.Items.Count == 0)
            throw new BadRequestException("Cannot invoice a sales order with no items.");

        var tenantId = _currentUserService.TenantId!.Value;
        var invoiceDate = DateTime.UtcNow;

        var invoice = new Invoice
        {
            TenantId = tenantId,
            InvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken),
            CustomerId = so.CustomerId,
            SalesOrderId = so.Id,
            InvoiceDate = invoiceDate,
            DueDate = request.DueDate ?? invoiceDate.AddDays(DefaultPaymentTermDays),
            // Generated from an agreed order, so it is immediately payable.
            Status = InvoiceStatus.Issued,
            Notes = $"Generated from sales order {so.OrderNumber}"
        };

        foreach (var soItem in so.Items)
        {
            invoice.Items.Add(BuildItem(
                tenantId, soItem.ProductId, soItem.Product?.Name ?? "Item",
                soItem.Quantity, soItem.UnitPrice, soItem.TaxRate, soItem.DiscountRate));
        }

        ApplyTotals(invoice);

        // Backfill the order's invoice number for cross-reference.
        so.InvoiceNumber = invoice.InvoiceNumber;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<InvoiceDto> IssueAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new BadRequestException($"Only draft invoices can be issued (current status '{invoice.Status}').");

        invoice.Status = InvoiceStatus.Issued;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.SalesOrder)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new BadRequestException("Invoice is already cancelled.");

        if (invoice.AmountPaid > 0)
            throw new BadRequestException("Cannot cancel an invoice that has payments applied. Void the payments first.");

        invoice.Status = InvoiceStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(invoice);
    }

    public async Task<List<OutstandingInvoiceDto>> GetOutstandingByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return await _context.Invoices
            .Where(i => i.CustomerId == customerId
                && !i.IsDeleted
                && (i.Status == InvoiceStatus.Issued
                    || i.Status == InvoiceStatus.PartiallyPaid
                    || i.Status == InvoiceStatus.Overdue)
                && i.TotalAmount > i.AmountPaid)
            .OrderBy(i => i.DueDate)
            .Select(i => new OutstandingInvoiceDto(
                i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate,
                i.TotalAmount, i.AmountPaid, i.TotalAmount - i.AmountPaid))
            .ToListAsync(cancellationToken);
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith($"INV-{today}"))
            .CountAsync(cancellationToken);
        return $"INV-{today}-{(todayCount + 1):D4}";
    }

    private static InvoiceItem BuildItem(
        Guid tenantId, Guid? productId, string description,
        int quantity, decimal unitPrice, decimal taxRate, decimal discountRate)
    {
        var lineSubTotal = quantity * unitPrice;
        var lineTax = lineSubTotal * (taxRate / 100m);
        var lineDiscount = lineSubTotal * (discountRate / 100m);

        return new InvoiceItem
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

    private static void ApplyTotals(Invoice invoice)
    {
        decimal subTotal = 0, tax = 0, discount = 0;
        foreach (var item in invoice.Items)
        {
            var lineSubTotal = item.Quantity * item.UnitPrice;
            subTotal += lineSubTotal;
            tax += lineSubTotal * (item.TaxRate / 100m);
            discount += lineSubTotal * (item.DiscountRate / 100m);
        }

        invoice.SubTotal = subTotal;
        invoice.TaxAmount = tax;
        invoice.DiscountAmount = discount;
        invoice.TotalAmount = subTotal + tax - discount;
    }

    private static InvoiceDto ToDto(Invoice i) => new(
        i.Id, i.InvoiceNumber, i.CustomerId, i.Customer.Name,
        i.SalesOrderId, i.SalesOrder != null ? i.SalesOrder.OrderNumber : null,
        i.InvoiceDate, i.DueDate, i.Status.ToString(),
        i.SubTotal, i.TaxAmount, i.DiscountAmount, i.TotalAmount, i.AmountPaid, i.BalanceDue, i.Notes,
        i.Items.Select(it => new InvoiceItemDto(
            it.Id, it.ProductId, it.Description, it.Quantity,
            it.UnitPrice, it.TaxRate, it.DiscountRate, it.LineTotal)).ToList());
}
