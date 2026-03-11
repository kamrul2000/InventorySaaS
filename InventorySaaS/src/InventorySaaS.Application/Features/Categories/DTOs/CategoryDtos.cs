namespace InventorySaaS.Application.Features.Categories.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive,
    int ProductCount);

public record CreateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId);

public record UpdateCategoryRequest(
    string? Name,
    string? Description,
    Guid? ParentCategoryId,
    bool? IsActive);
