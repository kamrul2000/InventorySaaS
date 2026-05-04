using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Tenants.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Tenant;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class TenantService : ITenantService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public TenantService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<TenantDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.Tenants
            .Include(t => t.SubscriptionPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(searchTerm) ||
                t.Slug.ToLower().Contains(searchTerm) ||
                (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(searchTerm)));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            "status" => pagination.SortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var projected = query.Select(t => new TenantDto(
            t.Id, t.Name, t.Slug, t.LogoUrl, t.ContactEmail,
            t.Status.ToString(), t.SubscriptionPlan.Name, t.CreatedAt));

        return await PaginatedList<TenantDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<TenantDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context not available.");

        var tenant = await _context.Tenants
            .Include(t => t.SubscriptionPlan)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(TenantInfo), tenantId);

        return new TenantDto(
            tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.ContactEmail,
            tenant.Status.ToString(), tenant.SubscriptionPlan.Name, tenant.CreatedAt);
    }

    public async Task<TenantDto> UpdateCurrentAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context not available.");

        var tenant = await _context.Tenants
            .Include(t => t.SubscriptionPlan)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(TenantInfo), tenantId);

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

        return new TenantDto(
            tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl, tenant.ContactEmail,
            tenant.Status.ToString(), tenant.SubscriptionPlan.Name, tenant.CreatedAt);
    }
}
