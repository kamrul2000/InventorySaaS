using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Suppliers.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Suppliers.Queries;

public record GetSuppliersQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<SupplierDto>>>;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, Result<PaginatedList<SupplierDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSuppliersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<SupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Suppliers
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchTerm) ||
                (s.Code != null && s.Code.ToLower().Contains(searchTerm)) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchTerm)));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.Pagination.SortDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
            _ => query.OrderBy(s => s.Name)
        };

        var projectedQuery = query.Select(s => new SupplierDto(
            s.Id,
            s.Name,
            s.Code,
            s.ContactPerson,
            s.Email,
            s.Phone,
            s.City,
            s.Country,
            s.IsActive));

        var result = await PaginatedList<SupplierDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<SupplierDto>>.Success(result);
    }
}
