using Tyto.Api.Application.Common.Constants;

namespace Tyto.Api.Application.Common;

public class QueryParameters
{
    private int _pageSize = PaginationDefaults.DefaultPageSize;
    private int _page = 1;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : value > PaginationDefaults.MaxPageSize ? PaginationDefaults.MaxPageSize : value;
    }

    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;
}
