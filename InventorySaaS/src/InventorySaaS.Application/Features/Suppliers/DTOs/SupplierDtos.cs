namespace InventorySaaS.Application.Features.Suppliers.DTOs;

public record SupplierDto(
    Guid Id,
    string Name,
    string? Code,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? City,
    string? Country,
    bool IsActive);

public record CreateSupplierRequest(
    string Name,
    string? Code,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Country,
    string? TaxId,
    string? PaymentTerms);

public record UpdateSupplierRequest(
    string? Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool? IsActive);
