using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Categories.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Categories.Commands;

public record UpdateCategoryCommand(
    Guid CategoryId,
    string? Name,
    string? Description,
    Guid? ParentCategoryId,
    bool? IsActive) : IRequest<Result<CategoryDto>>;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result<CategoryDto>.Failure("Category not found.");

        if (request.Name is not null) category.Name = request.Name;
        if (request.Description is not null) category.Description = request.Description;
        if (request.ParentCategoryId.HasValue) category.ParentCategoryId = request.ParentCategoryId.Value;
        if (request.IsActive.HasValue) category.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

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
