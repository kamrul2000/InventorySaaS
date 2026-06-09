namespace InventorySaaS.Application.Features.Billing.DTOs;

public record PaymentDto(
    Guid Id,
    string PaymentNumber,
    Guid CustomerId,
    string CustomerName,
    DateTime PaymentDate,
    decimal Amount,
    string Method,
    string? Reference,
    string? Notes,
    List<PaymentAllocationDto> Allocations);

public record PaymentAllocationDto(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount);

public record CreatePaymentRequest(
    Guid CustomerId,
    DateTime? PaymentDate,
    decimal Amount,
    string Method,
    string? Reference,
    string? Notes,
    List<CreatePaymentAllocationRequest> Allocations);

public record CreatePaymentAllocationRequest(
    Guid InvoiceId,
    decimal Amount);
