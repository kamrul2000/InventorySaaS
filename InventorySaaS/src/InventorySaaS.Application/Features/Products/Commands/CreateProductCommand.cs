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
    Guid? UnitOfMeasureId,
    decimal CostPrice,
    decimal SellingPrice,
    int ReorderLevel,
    string? Barcode,
    bool TrackExpiry,
    int MinimumOrderQuantity,
    string? BrandName,
    string? UnitName) : IRequest<Result<ProductDto>>;

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
        var tenantId = _currentUserService.TenantId!.Value;

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result<ProductDto>.Failure("Category not found.");

        // Resolve BrandId: use provided ID, or find/create by name
        Guid? brandId = request.BrandId;
        if (brandId is null && !string.IsNullOrWhiteSpace(request.BrandName))
        {
            var brand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Name == request.BrandName, cancellationToken);
            if (brand is null)
            {
                brand = new Brand { TenantId = tenantId, Name = request.BrandName, IsActive = true };
                _context.Brands.Add(brand);
                await _context.SaveChangesAsync(cancellationToken);
            }
            brandId = brand.Id;
        }

        // Resolve UnitOfMeasureId: use provided ID, or find/create by name
        Guid? unitId = request.UnitOfMeasureId;
        if (unitId is null && !string.IsNullOrWhiteSpace(request.UnitName))
        {
            var unit = await _context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Name == request.UnitName, cancellationToken);
            if (unit is null)
            {
                unit = new UnitOfMeasure { TenantId = tenantId, Name = request.UnitName, Abbreviation = request.UnitName.Length >= 3 ? request.UnitName[..3].ToLowerInvariant() : request.UnitName.ToLowerInvariant(), IsActive = true };
                _context.UnitsOfMeasure.Add(unit);
                await _context.SaveChangesAsync(cancellationToken);
            }
            unitId = unit.Id;
        }

        if (unitId is null)
            return Result<ProductDto>.Failure("Unit of Measure is required. Provide UnitOfMeasureId or UnitName.");

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
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Sku = sku,
            Barcode = request.Barcode,
            CategoryId = request.CategoryId,
            BrandId = brandId,
            UnitOfMeasureId = unitId.Value,
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
