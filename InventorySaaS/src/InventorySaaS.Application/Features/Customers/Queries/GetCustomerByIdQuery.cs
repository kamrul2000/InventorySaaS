using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Customers.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Customers.Queries;

public record GetCustomerByIdQuery(Guid CustomerId) : IRequest<Result<CustomerDto>>;

public class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .Where(c => c.Id == request.CustomerId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
            return Result<CustomerDto>.Failure("Customer not found.");

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
