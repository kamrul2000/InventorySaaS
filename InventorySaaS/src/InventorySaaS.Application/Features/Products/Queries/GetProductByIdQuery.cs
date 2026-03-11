using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Products.Queries;

public record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .Where(p => p.Id == request.ProductId && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return Result<ProductDto>.Failure("Product not found.");

        var dto = new ProductDto(
            product.Id,
            product.Name,
            product.Sku,
            product.Barcode,
            product.Category.Name,
            product.Brand?.Name,
            product.UnitOfMeasure.Name,
            product.CostPrice,
            product.SellingPrice,
            product.ReorderLevel,
            product.TrackExpiry,
            product.IsActive,
            product.CreatedAt);

        return Result<ProductDto>.Success(dto);
    }
}
