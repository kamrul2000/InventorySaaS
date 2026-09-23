using FluentValidation;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Customers.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;

    public CustomerService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator)
    {
        _context = context;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PaginatedList<CustomerDto>> GetAllAsync(
        PaginationParams pagination,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = _context.Customers
            .Where(c => !c.IsDeleted)
            .AsQueryable();

        if (isActive.HasValue) query = query.Where(c => c.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchTerm) ||
                (c.Code != null && c.Code.ToLower().Contains(searchTerm)) ||
                (c.ContactPerson != null && c.ContactPerson.ToLower().Contains(searchTerm)));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending
                ? query.OrderByDescending(c => c.Name)
                : query.OrderBy(c => c.Name),
            _ => query.OrderBy(c => c.Name)
        };

        var projected = query.Select(c => new CustomerDto(
            c.Id,
            c.Name,
            c.Code,
            c.CustomerType,
            c.ContactPerson,
            c.Email,
            c.Phone,
            c.Address,
            c.City,
            c.Country,
            c.TaxId,
            c.PaymentTerms,
            c.CreditLimit,
            c.IsActive));

        return await PaginatedList<CustomerDto>.CreateAsync(
            projected,
            pagination.PageNumber,
            pagination.PageSize,
            cancellationToken);
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
            throw new NotFoundException(nameof(CustomerInfo), id);

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Code,
            customer.CustomerType,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.Country,
            customer.TaxId,
            customer.PaymentTerms,
            customer.CreditLimit,
            customer.IsActive);
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = new CustomerInfo
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = request.Name,
            Code = request.Code,
            CustomerType = request.CustomerType,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            TaxId = request.TaxId,
            PaymentTerms = request.PaymentTerms,
            CreditLimit = request.CreditLimit,
            IsActive = true
        };

        _context.Customers.Add(customer);
        await SaveOrThrowConflictAsync(cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Code,
            customer.CustomerType,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.Country,
            customer.TaxId,
            customer.PaymentTerms,
            customer.CreditLimit,
            customer.IsActive);
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer is null)
            throw new NotFoundException(nameof(CustomerInfo), id);

        if (request.Name is not null) customer.Name = request.Name;
        if (request.Code is not null) customer.Code = request.Code;
        if (request.CustomerType is not null) customer.CustomerType = request.CustomerType;
        if (request.ContactPerson is not null) customer.ContactPerson = request.ContactPerson;
        if (request.Email is not null) customer.Email = request.Email;
        if (request.Phone is not null) customer.Phone = request.Phone;
        if (request.Address is not null) customer.Address = request.Address;
        if (request.City is not null) customer.City = request.City;
        if (request.Country is not null) customer.Country = request.Country;
        if (request.TaxId is not null) customer.TaxId = request.TaxId;
        if (request.PaymentTerms is not null) customer.PaymentTerms = request.PaymentTerms;
        if (request.CreditLimit.HasValue) customer.CreditLimit = request.CreditLimit.Value;
        if (request.IsActive.HasValue) customer.IsActive = request.IsActive.Value;

        await SaveOrThrowConflictAsync(cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Code,
            customer.CustomerType,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.City,
            customer.Country,
            customer.TaxId,
            customer.PaymentTerms,
            customer.CreditLimit,
            customer.IsActive);
    }

    /// <summary>
    /// The unique filtered index on (TenantId, Code) is the real safety net against a duplicate
    /// code (DATA-01); this turns that DB-level violation into a clean 409 instead of a 500.
    /// </summary>
    private async Task SaveOrThrowConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("A customer with this code already exists.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerInfo), id);

        var hasOpenOrders = await _context.SalesOrders
            .AnyAsync(so => so.CustomerId == id && !so.IsDeleted, cancellationToken);

        if (hasOpenOrders)
            throw new ConflictException("Cannot delete a customer with existing sales orders.");

        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        customer.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
