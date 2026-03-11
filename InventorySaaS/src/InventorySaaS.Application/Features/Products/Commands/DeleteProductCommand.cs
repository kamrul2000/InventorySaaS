using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Products.Commands;

public record DeleteProductCommand(Guid ProductId) : IRequest<Result>;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found.");

        // Check if product has inventory
        var hasInventory = await _context.InventoryBalances
            .AnyAsync(ib => ib.ProductId == request.ProductId && ib.QuantityOnHand > 0, cancellationToken);

        if (hasInventory)
            return Result.Failure("Cannot delete a product that has inventory on hand. Please remove all stock first.");

        // Soft delete
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        product.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
