using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Products.Queries;

public record GetProductsQuery(
    PaginationParams Pagination,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    bool? IsActive = null) : IRequest<Result<PaginatedList<ProductDto>>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<PaginatedList<ProductDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // Filters
        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.BrandId.HasValue)
            query = query.Where(p => p.BrandId == request.BrandId.Value);

        if (request.IsActive.HasValue)
            query = query.Where(p => p.IsActive == request.IsActive.Value);

        // Search
        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.Sku.ToLower().Contains(searchTerm) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(searchTerm)));
        }

        // Sort
        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.Pagination.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "sku" => request.Pagination.SortDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "price" => request.Pagination.SortDescending ? query.OrderByDescending(p => p.SellingPrice) : query.OrderBy(p => p.SellingPrice),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var projectedQuery = query.Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.Sku,
            p.Barcode,
            p.Category.Name,
            p.Brand != null ? p.Brand.Name : null,
            p.UnitOfMeasure.Name,
            p.CostPrice,
            p.SellingPrice,
            p.ReorderLevel,
            p.TrackExpiry,
            p.IsActive,
            p.CreatedAt));

        var result = await PaginatedList<ProductDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<ProductDto>>.Success(result);
    }
}
