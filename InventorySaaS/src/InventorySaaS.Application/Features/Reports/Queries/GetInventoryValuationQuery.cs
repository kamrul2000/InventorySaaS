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
        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Include(ib => ib.Product).ThenInclude(p => p.Category)
            .Where(ib => ib.QuantityOnHand > 0)
            .ToListAsync(cancellationToken);

        var valuations = balances
            .GroupBy(ib => ib.Product.Category?.Name ?? "Uncategorized")
            .Select(g => new InventoryValuationDto(
                g.Key,
                g.Select(ib => ib.ProductId).Distinct().Count(),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.UnitCost),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.Product.SellingPrice)))
            .OrderByDescending(v => v.TotalCostValue)
            .ToList();

        return Result<List<InventoryValuationDto>>.Success(valuations);
    }
}
