namespace InventorySaaS.Application.Features.Customers.DTOs;

public record CustomerDto(
    Guid Id,
    string Name,
    string? Code,
    string? CustomerType,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? City,
    string? Country,
    bool IsActive);

public record CreateCustomerRequest(
    string Name,
    string? Code,
    string? CustomerType,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    string? TaxId,
    string? PaymentTerms,
    decimal? CreditLimit);

public record UpdateCustomerRequest(
    string? Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool? IsActive);
