using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Products.Commands;

public record CreateProductCommand(
    string Name,
    string? Description,
    Guid CategoryId,
    Guid? BrandId,
    Guid UnitOfMeasureId,
    decimal CostPrice,
    decimal SellingPrice,
    int ReorderLevel,
    string? Barcode,
    bool TrackExpiry,
    int MinimumOrderQuantity) : IRequest<Result<ProductDto>>;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateProductCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result<ProductDto>.Failure("Category not found.");

        // Auto-generate SKU: {CategoryCode}-{sequential number}
        var categoryCode = category.Name.Length >= 3
            ? category.Name[..3].ToUpperInvariant()
            : category.Name.ToUpperInvariant().PadRight(3, 'X');

        var existingCount = await _context.Products
            .Where(p => p.CategoryId == request.CategoryId)
            .CountAsync(cancellationToken);

        var sku = $"{categoryCode}-{(existingCount + 1):D5}";

        var product = new ProductInfo
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = request.Name,
            Description = request.Description,
            Sku = sku,
            Barcode = request.Barcode,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            UnitOfMeasureId = request.UnitOfMeasureId,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            ReorderLevel = request.ReorderLevel,
            TrackExpiry = request.TrackExpiry,
            MinimumOrderQuantity = request.MinimumOrderQuantity,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with navigations
        var savedProduct = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .FirstAsync(p => p.Id == product.Id, cancellationToken);

        var dto = new ProductDto(
            savedProduct.Id,
            savedProduct.Name,
            savedProduct.Sku,
            savedProduct.Barcode,
            savedProduct.Category.Name,
            savedProduct.Brand?.Name,
            savedProduct.UnitOfMeasure.Name,
            savedProduct.CostPrice,
            savedProduct.SellingPrice,
            savedProduct.ReorderLevel,
            savedProduct.TrackExpiry,
            savedProduct.IsActive,
            savedProduct.CreatedAt);

        return Result<ProductDto>.Success(dto);
    }
}
