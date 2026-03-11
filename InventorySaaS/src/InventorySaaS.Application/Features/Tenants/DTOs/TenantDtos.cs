namespace InventorySaaS.Application.Features.Tenants.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? ContactEmail,
    string Status,
    string PlanName,
    DateTime CreatedAt);

public record UpdateTenantRequest(
    string? Name,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? City,
    string? Country,
    string? Currency,
    string? Timezone,
    string? LogoUrl);

public record TenantSettingsDto(
    string? Currency,
    string? Timezone,
    string? LogoUrl);
