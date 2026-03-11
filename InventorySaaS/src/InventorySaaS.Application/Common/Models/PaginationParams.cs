namespace InventorySaaS.Application.Common.Models;

public record PaginationParams(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? SortBy = null,
    bool SortDescending = false);
