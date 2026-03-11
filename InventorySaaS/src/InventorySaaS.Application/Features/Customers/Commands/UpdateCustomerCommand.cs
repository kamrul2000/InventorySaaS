using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Customers.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Customers.Commands;

public record UpdateCustomerCommand(
    Guid CustomerId,
    string? Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool? IsActive) : IRequest<Result<CustomerDto>>;

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            return Result<CustomerDto>.Failure("Customer not found.");

        if (request.Name is not null) customer.Name = request.Name;
        if (request.ContactPerson is not null) customer.ContactPerson = request.ContactPerson;
        if (request.Email is not null) customer.Email = request.Email;
        if (request.Phone is not null) customer.Phone = request.Phone;
        if (request.Address is not null) customer.Address = request.Address;
        if (request.IsActive.HasValue) customer.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Code,
            customer.CustomerType,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.City,
            customer.Country,
            customer.IsActive);

        return Result<CustomerDto>.Success(dto);
    }
}
