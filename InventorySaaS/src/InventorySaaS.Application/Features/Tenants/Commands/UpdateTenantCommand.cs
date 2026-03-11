using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Tenants.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Tenants.Commands;

public record UpdateTenantCommand(
    string? Name,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? City,
    string? Country,
    string? Currency,
    string? Timezone,
    string? LogoUrl) : IRequest<Result<TenantDto>>;

public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, Result<TenantDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateTenantCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TenantDto>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null)
            return Result<TenantDto>.Failure("Tenant context not available.");

        var tenant = await _context.Tenants
            .Include(t => t.SubscriptionPlan)
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        if (tenant is null)
            return Result<TenantDto>.Failure("Tenant not found.");

        if (request.Name is not null) tenant.Name = request.Name;
        if (request.ContactEmail is not null) tenant.ContactEmail = request.ContactEmail;
        if (request.ContactPhone is not null) tenant.ContactPhone = request.ContactPhone;
        if (request.Address is not null) tenant.Address = request.Address;
        if (request.City is not null) tenant.City = request.City;
        if (request.Country is not null) tenant.Country = request.Country;
        if (request.Currency is not null) tenant.Currency = request.Currency;
        if (request.Timezone is not null) tenant.Timezone = request.Timezone;
        if (request.LogoUrl is not null) tenant.LogoUrl = request.LogoUrl;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.LogoUrl,
            tenant.ContactEmail,
            tenant.Status.ToString(),
            tenant.SubscriptionPlan.Name,
            tenant.CreatedAt);

        return Result<TenantDto>.Success(dto);
    }
}
