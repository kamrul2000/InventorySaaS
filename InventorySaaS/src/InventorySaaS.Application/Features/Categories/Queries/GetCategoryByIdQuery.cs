using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Categories.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Categories.Queries;

public record GetCategoryByIdQuery(Guid CategoryId) : IRequest<Result<CategoryDto>>;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .Where(c => c.Id == request.CategoryId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
            return Result<CategoryDto>.Failure("Category not found.");

        var dto = new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.ParentCategoryId,
            category.IsActive,
            category.Products.Count);

        return Result<CategoryDto>.Success(dto);
    }
}
