using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Reports.Queries;

public record GetInventoryValuationQuery : IRequest<Result<List<InventoryValuationDto>>>;

public class GetInventoryValuationQueryHandler : IRequestHandler<GetInventoryValuationQuery, Result<List<InventoryValuationDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryValuationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<InventoryValuationDto>>> Handle(GetInventoryValuationQuery request, CancellationToken cancellationToken)
    {
        var valuations = await _context.InventoryBalances
            .AsNoTracking()
            .Where(ib => ib.QuantityOnHand > 0)
            .GroupBy(ib => ib.Product.Category != null ? ib.Product.Category.Name : "Uncategorized")
            .Select(g => new InventoryValuationDto(
                g.Key,
                g.Select(ib => ib.ProductId).Distinct().Count(),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.UnitCost),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.Product.SellingPrice)))
            .OrderByDescending(v => v.TotalCostValue)
            .ToListAsync(cancellationToken);

        return Result<List<InventoryValuationDto>>.Success(valuations);
    }
}
