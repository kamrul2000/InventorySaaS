using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Suppliers.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Suppliers.Queries;

public record GetSupplierByIdQuery(Guid SupplierId) : IRequest<Result<SupplierDto>>;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, Result<SupplierDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSupplierByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SupplierDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .Where(s => s.Id == request.SupplierId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier is null)
            return Result<SupplierDto>.Failure("Supplier not found.");

        var dto = new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Code,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.City,
            supplier.Country,
            supplier.IsActive);

        return Result<SupplierDto>.Success(dto);
    }
}
