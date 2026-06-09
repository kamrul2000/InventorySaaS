namespace InventorySaaS.Application.Features.Billing.DTOs;

public record SupplierPaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid SupplierId,
    string SupplierName,
    DateTime PaymentDate,
    decimal Amount,
    string Method,
    string? Reference,
    string? Notes,
    List<SupplierPaymentAllocationDto> Allocations);

public record SupplierPaymentAllocationDto(
    Guid BillId,
    string BillNumber,
    decimal Amount);

public record CreateSupplierPaymentRequest(
    Guid SupplierId,
    DateTime? PaymentDate,
    decimal Amount,
    string Method,
    string? Reference,
    string? Notes,
    List<CreateSupplierPaymentAllocationRequest> Allocations);

public record CreateSupplierPaymentAllocationRequest(
    Guid BillId,
    decimal Amount);
