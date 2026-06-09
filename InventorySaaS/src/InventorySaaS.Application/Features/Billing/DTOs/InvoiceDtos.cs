namespace InventorySaaS.Application.Features.Billing.DTOs;

public record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    string Status,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    string? Notes,
    List<InvoiceItemDto> Items);

public record InvoiceItemDto(
    Guid Id,
    Guid? ProductId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate,
    decimal LineTotal);

public record CreateInvoiceRequest(
    Guid CustomerId,
    DateTime? DueDate,
    string? Notes,
    List<CreateInvoiceItemRequest> Items);

public record CreateInvoiceItemRequest(
    Guid? ProductId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate);

public record CreateInvoiceFromSalesOrderRequest(
    Guid SalesOrderId,
    DateTime? DueDate);

/// <summary>Lightweight view of an invoice with an outstanding balance, for payment allocation UIs.</summary>
public record OutstandingInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue);
