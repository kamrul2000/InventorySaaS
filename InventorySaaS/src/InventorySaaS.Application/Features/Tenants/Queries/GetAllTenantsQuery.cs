using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Tenants.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Tenants.Queries;

/// <summary>
/// SuperAdmin-only query to list all tenants with pagination.
/// Authorization should be enforced at the API/controller level.
/// </summary>
public record GetAllTenantsQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<TenantDto>>>;

public class GetAllTenantsQueryHandler : IRequestHandler<GetAllTenantsQuery, Result<PaginatedList<TenantDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllTenantsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<TenantDto>>> Handle(GetAllTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tenants
            .Include(t => t.SubscriptionPlan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(searchTerm) ||
                t.Slug.ToLower().Contains(searchTerm) ||
                (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(searchTerm)));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.Pagination.SortDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            "status" => request.Pagination.SortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var projectedQuery = query.Select(t => new TenantDto(
            t.Id,
            t.Name,
            t.Slug,
            t.LogoUrl,
            t.ContactEmail,
            t.Status.ToString(),
            t.SubscriptionPlan.Name,
            t.CreatedAt));

        var result = await PaginatedList<TenantDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<TenantDto>>.Success(result);
    }
}
