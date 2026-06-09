namespace InventorySaaS.Application.Features.Billing.DTOs;

public record SupplierBillDto(
    Guid Id,
    string BillNumber,
    Guid SupplierId,
    string SupplierName,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    string? SupplierInvoiceNumber,
    DateTime BillDate,
    DateTime DueDate,
    string Status,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue,
    string? Notes,
    List<SupplierBillItemDto> Items);

public record SupplierBillItemDto(
    Guid Id,
    Guid? ProductId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate,
    decimal LineTotal);

public record CreateSupplierBillRequest(
    Guid SupplierId,
    string? SupplierInvoiceNumber,
    DateTime? DueDate,
    string? Notes,
    List<CreateSupplierBillItemRequest> Items);

public record CreateSupplierBillItemRequest(
    Guid? ProductId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountRate);

public record CreateBillFromPurchaseOrderRequest(
    Guid PurchaseOrderId,
    string? SupplierInvoiceNumber,
    DateTime? DueDate);

/// <summary>Lightweight view of a bill with an outstanding balance, for payment allocation UIs.</summary>
public record OutstandingBillDto(
    Guid Id,
    string BillNumber,
    DateTime BillDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal BalanceDue);
