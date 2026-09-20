using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Brands.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class BrandService : IBrandService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BrandService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<BrandDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.Brands
            .Include(b => b.Products)
            .Where(b => !b.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(b => b.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending
                ? query.OrderByDescending(b => b.Name)
                : query.OrderBy(b => b.Name),
            _ => query.OrderBy(b => b.Name)
        };

        var projected = query.Select(b => new BrandDto(
            b.Id,
            b.Name,
            b.Description,
            b.LogoUrl,
            b.IsActive,
            b.Products.Count(p => !p.IsDeleted)));

        return await PaginatedList<BrandDto>.CreateAsync(
            projected,
            pagination.PageNumber,
            pagination.PageSize,
            cancellationToken);
    }

    public async Task<BrandDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .Include(b => b.Products)
            .Where(b => b.Id == id && !b.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            throw new NotFoundException(nameof(Brand), id);

        return ToDto(brand);
    }

    public async Task<BrandDto> CreateAsync(
        CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Brand name is required.");

        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var brand = new Brand
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = name,
            Description = request.Description,
            LogoUrl = request.LogoUrl,
            IsActive = true
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandDto(brand.Id, brand.Name, brand.Description, brand.LogoUrl, brand.IsActive, 0);
    }

    public async Task<BrandDto> UpdateAsync(
        Guid id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .Include(b => b.Products)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), id);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            await EnsureNameIsUniqueAsync(name, id, cancellationToken);
            brand.Name = name;
        }

        if (request.Description is not null) brand.Description = request.Description;
        if (request.LogoUrl is not null) brand.LogoUrl = request.LogoUrl;
        if (request.IsActive.HasValue) brand.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(brand);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .Include(b => b.Products)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Brand), id);

        if (brand.Products.Any(p => !p.IsDeleted))
            throw new ConflictException("Cannot delete a brand that has products. Reassign or delete the products first.");

        brand.IsDeleted = true;
        brand.DeletedAt = DateTime.UtcNow;
        brand.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Brand names are the user-facing key for this master list, so a tenant may not hold two.
    /// Compared case-insensitively: "Nestle" and "nestle" are the same brand to a warehouse clerk.
    /// </summary>
    private async Task EnsureNameIsUniqueAsync(
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();

        var duplicateExists = await _context.Brands
            .AnyAsync(b => !b.IsDeleted
                && b.Name.ToLower() == lowered
                && (excludeId == null || b.Id != excludeId), cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A brand named '{name}' already exists.");
    }

    private static BrandDto ToDto(Brand brand) => new(
        brand.Id,
        brand.Name,
        brand.Description,
        brand.LogoUrl,
        brand.IsActive,
        brand.Products.Count(p => !p.IsDeleted));
}
