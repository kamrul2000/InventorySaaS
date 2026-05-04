using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class ProductService : IProductService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ProductService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<ProductDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? categoryId,
        Guid? brandId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId.Value);
        if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.Sku.ToLower().Contains(searchTerm) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(searchTerm)));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "sku" => pagination.SortDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "price" => pagination.SortDescending ? query.OrderByDescending(p => p.SellingPrice) : query.OrderBy(p => p.SellingPrice),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var projected = query.Select(p => new ProductDto(
            p.Id, p.Name, p.Sku, p.Barcode,
            p.Category.Name, p.Brand != null ? p.Brand.Name : null, p.UnitOfMeasure.Name,
            p.CostPrice, p.SellingPrice, p.ReorderLevel, p.TrackExpiry, p.IsActive, p.CreatedAt));

        return await PaginatedList<ProductDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), id);

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Category.Name, product.Brand?.Name, product.UnitOfMeasure.Name,
            product.CostPrice, product.SellingPrice, product.ReorderLevel,
            product.TrackExpiry, product.IsActive, product.CreatedAt);
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId!.Value;

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new BadRequestException("Category not found.");

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

        Guid? unitId = request.UnitOfMeasureId;
        if (unitId is null && !string.IsNullOrWhiteSpace(request.UnitName))
        {
            var unit = await _context.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Name == request.UnitName, cancellationToken);
            if (unit is null)
            {
                unit = new UnitOfMeasure
                {
                    TenantId = tenantId,
                    Name = request.UnitName,
                    Abbreviation = request.UnitName.Length >= 3
                        ? request.UnitName[..3].ToLowerInvariant()
                        : request.UnitName.ToLowerInvariant(),
                    IsActive = true
                };
                _context.UnitsOfMeasure.Add(unit);
                await _context.SaveChangesAsync(cancellationToken);
            }
            unitId = unit.Id;
        }

        if (unitId is null)
            throw new BadRequestException("Unit of Measure is required. Provide UnitOfMeasureId or UnitName.");

        var categoryCode = category.Name.Length >= 3
            ? category.Name[..3].ToUpperInvariant()
            : category.Name.ToUpperInvariant().PadRight(3, 'X');

        var prefix = $"{categoryCode}-";
        var existingSkus = await _context.Products
            .Where(p => p.Sku.StartsWith(prefix))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        var nextNumber = 1;
        foreach (var existing in existingSkus)
        {
            if (int.TryParse(existing[prefix.Length..], out var n) && n >= nextNumber)
                nextNumber = n + 1;
        }

        var sku = $"{prefix}{nextNumber:D5}";

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
            ReorderLevel = request.ReorderLevel ?? 0,
            TrackExpiry = request.TrackExpiry,
            MinimumOrderQuantity = request.MinimumOrderQuantity ?? 1,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        var saved = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .FirstAsync(p => p.Id == product.Id, cancellationToken);

        return new ProductDto(
            saved.Id, saved.Name, saved.Sku, saved.Barcode,
            saved.Category.Name, saved.Brand?.Name, saved.UnitOfMeasure.Name,
            saved.CostPrice, saved.SellingPrice, saved.ReorderLevel,
            saved.TrackExpiry, saved.IsActive, saved.CreatedAt);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), id);

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

        if (request.CategoryId.HasValue || request.BrandId.HasValue || request.UnitOfMeasureId.HasValue)
        {
            product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.UnitOfMeasure)
                .FirstAsync(p => p.Id == product.Id, cancellationToken);
        }

        return new ProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Category.Name, product.Brand?.Name, product.UnitOfMeasure.Name,
            product.CostPrice, product.SellingPrice, product.ReorderLevel,
            product.TrackExpiry, product.IsActive, product.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), id);

        var hasInventory = await _context.InventoryBalances
            .AnyAsync(ib => ib.ProductId == id && ib.QuantityOnHand > 0, cancellationToken);

        if (hasInventory)
            throw new ConflictException("Cannot delete a product that has inventory on hand. Please remove all stock first.");

        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
