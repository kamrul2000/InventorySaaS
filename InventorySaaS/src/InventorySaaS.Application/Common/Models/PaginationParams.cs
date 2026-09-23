namespace InventorySaaS.Application.Common.Models;

public class PaginationParams
{
    // Report/PDF export endpoints deliberately request pageSize=10000 to fetch a full unpaginated
    // dataset in one call (see report.service.ts's REPORT_PAGE_SIZE and the *Pdf controller
    // actions) - the cap has to accommodate that existing convention rather than clip it, while
    // still rejecting a genuinely unbounded request (RPT-04).
    public const int MaxPageSize = 10_000;

    private readonly int _pageNumber;
    private readonly int _pageSize;

    // A zero/negative PageNumber or PageSize used to flow straight into Skip()/Take() and throw
    // an unhandled ArgumentOutOfRangeException (500) instead of a clean validation error (RPT-03);
    // an unbounded PageSize could also force a full-table materialization (RPT-04). Both are
    // clamped here - once, for every caller - rather than validated ad hoc per controller.
    public int PageNumber { get => _pageNumber; init => _pageNumber = value < 1 ? 1 : value; }
    public int PageSize { get => _pageSize; init => _pageSize = value < 1 ? 1 : Math.Min(value, MaxPageSize); }
    public string? SearchTerm { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public PaginationParams(
        int pageNumber = 1,
        int pageSize = 20,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        SearchTerm = searchTerm;
        SortBy = sortBy;
        SortDescending = sortDescending;
    }
}
