using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Suppliers.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class SupplierService : ISupplierService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SupplierService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<SupplierDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.Suppliers
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchTerm) ||
                (s.Code != null && s.Code.ToLower().Contains(searchTerm)) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchTerm)));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending
                ? query.OrderByDescending(s => s.Name)
                : query.OrderBy(s => s.Name),
            _ => query.OrderBy(s => s.Name)
        };

        var projected = query.Select(s => new SupplierDto(
            s.Id,
            s.Name,
            s.Code,
            s.ContactPerson,
            s.Email,
            s.Phone,
            s.City,
            s.Country,
            s.IsActive));

        return await PaginatedList<SupplierDto>.CreateAsync(
            projected,
            pagination.PageNumber,
            pagination.PageSize,
            cancellationToken);
    }

    public async Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .Where(s => s.Id == id && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier is null)
            throw new NotFoundException(nameof(SupplierInfo), id);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Code,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.City,
            supplier.Country,
            supplier.IsActive);
    }

    public async Task<SupplierDto> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = new SupplierInfo
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = request.Name,
            Code = request.Code,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            TaxId = request.TaxId,
            PaymentTerms = request.PaymentTerms,
            IsActive = true
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Code,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.City,
            supplier.Country,
            supplier.IsActive);
    }

    public async Task<SupplierDto> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
            throw new NotFoundException(nameof(SupplierInfo), id);

        if (request.Name is not null) supplier.Name = request.Name;
        if (request.Code is not null) supplier.Code = request.Code;
        if (request.ContactPerson is not null) supplier.ContactPerson = request.ContactPerson;
        if (request.Email is not null) supplier.Email = request.Email;
        if (request.Phone is not null) supplier.Phone = request.Phone;
        if (request.Address is not null) supplier.Address = request.Address;
        if (request.City is not null) supplier.City = request.City;
        if (request.Country is not null) supplier.Country = request.Country;
        if (request.TaxId is not null) supplier.TaxId = request.TaxId;
        if (request.PaymentTerms is not null) supplier.PaymentTerms = request.PaymentTerms;
        if (request.IsActive.HasValue) supplier.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Code,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.City,
            supplier.Country,
            supplier.IsActive);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierInfo), id);

        var hasOpenOrders = await _context.PurchaseOrders
            .AnyAsync(po => po.SupplierId == id && !po.IsDeleted, cancellationToken);

        if (hasOpenOrders)
            throw new ConflictException("Cannot delete a supplier with existing purchase orders.");

        supplier.IsDeleted = true;
        supplier.DeletedAt = DateTime.UtcNow;
        supplier.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
