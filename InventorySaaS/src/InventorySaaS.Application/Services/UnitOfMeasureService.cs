using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Units.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class UnitOfMeasureService : IUnitOfMeasureService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UnitOfMeasureService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<UnitOfMeasureDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.UnitsOfMeasure
            .Include(u => u.Products)
            .Where(u => !u.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(u =>
                u.Name.ToLower().Contains(searchTerm) ||
                u.Abbreviation.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending
                ? query.OrderByDescending(u => u.Name)
                : query.OrderBy(u => u.Name),
            _ => query.OrderBy(u => u.Name)
        };

        var projected = query.Select(u => new UnitOfMeasureDto(
            u.Id,
            u.Name,
            u.Abbreviation,
            u.IsActive,
            u.Products.Count(p => !p.IsDeleted)));

        return await PaginatedList<UnitOfMeasureDto>.CreateAsync(
            projected,
            pagination.PageNumber,
            pagination.PageSize,
            cancellationToken);
    }

    public async Task<UnitOfMeasureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure
            .Include(u => u.Products)
            .Where(u => u.Id == id && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (unit is null)
            throw new NotFoundException(nameof(UnitOfMeasure), id);

        return ToDto(unit);
    }

    public async Task<UnitOfMeasureDto> CreateAsync(
        CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Unit name is required.");

        var name = request.Name.Trim();
        var abbreviation = NormaliseAbbreviation(request.Abbreviation, name);

        await EnsureIsUniqueAsync(name, abbreviation, null, cancellationToken);

        var unit = new UnitOfMeasure
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = name,
            Abbreviation = abbreviation,
            IsActive = true
        };

        _context.UnitsOfMeasure.Add(unit);
        await _context.SaveChangesAsync(cancellationToken);

        return new UnitOfMeasureDto(unit.Id, unit.Name, unit.Abbreviation, unit.IsActive, 0);
    }

    public async Task<UnitOfMeasureDto> UpdateAsync(
        Guid id,
        UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure
            .Include(u => u.Products)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(UnitOfMeasure), id);

        var name = string.IsNullOrWhiteSpace(request.Name) ? unit.Name : request.Name.Trim();
        var abbreviation = string.IsNullOrWhiteSpace(request.Abbreviation)
            ? unit.Abbreviation
            : request.Abbreviation.Trim().ToLowerInvariant();

        if (name != unit.Name || abbreviation != unit.Abbreviation)
            await EnsureIsUniqueAsync(name, abbreviation, id, cancellationToken);

        unit.Name = name;
        unit.Abbreviation = abbreviation;
        if (request.IsActive.HasValue) unit.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(unit);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var unit = await _context.UnitsOfMeasure
            .Include(u => u.Products)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(UnitOfMeasure), id);

        if (unit.Products.Any(p => !p.IsDeleted))
            throw new ConflictException("Cannot delete a unit that is used by products. Reassign or delete the products first.");

        unit.IsDeleted = true;
        unit.DeletedAt = DateTime.UtcNow;
        unit.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mirrors the fallback <see cref="ProductService"/> uses when it auto-creates a unit from a
    /// typed-in name, so a unit created either way ends up with the same abbreviation.
    /// </summary>
    private static string NormaliseAbbreviation(string? abbreviation, string name)
    {
        if (!string.IsNullOrWhiteSpace(abbreviation))
            return abbreviation.Trim().ToLowerInvariant();

        return name.Length >= 3
            ? name[..3].ToLowerInvariant()
            : name.ToLowerInvariant();
    }

    /// <summary>
    /// Both name and abbreviation are user-facing keys for this master list — two units reading
    /// "pcs" in a dropdown are indistinguishable to whoever is picking one.
    /// </summary>
    private async Task EnsureIsUniqueAsync(
        string name,
        string abbreviation,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var loweredName = name.ToLowerInvariant();

        var nameTaken = await _context.UnitsOfMeasure
            .AnyAsync(u => !u.IsDeleted
                && u.Name.ToLower() == loweredName
                && (excludeId == null || u.Id != excludeId), cancellationToken);

        if (nameTaken)
            throw new ConflictException($"A unit named '{name}' already exists.");

        var abbreviationTaken = await _context.UnitsOfMeasure
            .AnyAsync(u => !u.IsDeleted
                && u.Abbreviation.ToLower() == abbreviation
                && (excludeId == null || u.Id != excludeId), cancellationToken);

        if (abbreviationTaken)
            throw new ConflictException($"A unit with the abbreviation '{abbreviation}' already exists.");
    }

    private static UnitOfMeasureDto ToDto(UnitOfMeasure unit) => new(
        unit.Id,
        unit.Name,
        unit.Abbreviation,
        unit.IsActive,
        unit.Products.Count(p => !p.IsDeleted));
}
