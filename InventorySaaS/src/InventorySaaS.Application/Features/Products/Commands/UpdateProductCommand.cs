using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Products.Commands;

public record UpdateProductCommand(
    Guid ProductId,
    string? Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    Guid? UnitOfMeasureId,
    decimal? CostPrice,
    decimal? SellingPrice,
    int? ReorderLevel,
    string? Barcode,
    bool? TrackExpiry,
    bool? IsActive) : IRequest<Result<ProductDto>>;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            return Result<ProductDto>.Failure("Product not found.");

        if (request.Name is not null) product.Name = request.Name;
        if (request.Description is not null) product.Description = request.Description;
        if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
        if (request.BrandId.HasValue) product.BrandId = request.BrandId.Value;
        if (request.UnitOfMeasureId.HasValue) product.UnitOfMeasureId = request.UnitOfMeasureId.Value;
        if (request.CostPrice.HasValue) product.CostPrice = request.CostPrice.Value;
        if (request.SellingPrice.HasValue) product.SellingPrice = request.SellingPrice.Value;
        if (request.ReorderLevel.HasValue) product.ReorderLevel = request.ReorderLevel.Value;
        if (request.Barcode is not null) product.Barcode = request.Barcode;
        if (request.TrackExpiry.HasValue) product.TrackExpiry = request.TrackExpiry.Value;
        if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        // Reload navigations if category/brand/unit changed
        if (request.CategoryId.HasValue || request.BrandId.HasValue || request.UnitOfMeasureId.HasValue)
        {
            product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.UnitOfMeasure)
                .FirstAsync(p => p.Id == product.Id, cancellationToken);
        }

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
