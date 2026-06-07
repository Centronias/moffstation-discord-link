using System.Collections.Immutable;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace MoffDiscordLink.Pagination;

public interface IPaginationState
{
    public IDictionary<string, string?> AllRouteData { get; }
    bool HasNextPage { get; }
    bool HasPrevPage { get; }
    int PageIndex { get; }
    int PerPage { get; }
    int? TotalCount { get; }
    int DefaultPerPage { get; }
}

public class PaginationState<T> : IPaginationState
{
    public bool ShowTotal { get; }
    public int DefaultPerPage { get; }
    public int PageIndex { get; private set; }
    public int PerPage { get; private set; }
    public int? TotalCount => List.TotalCount;
    public PaginatedList<T> List { get; private set; } = default!;
    public bool HasPrevPage => List.HasPrevPage;
    public bool HasNextPage => List.HasNextPage;

    public IDictionary<string, string?> AllRouteData { get; private set; } = ImmutableDictionary<string, string?>.Empty;

    public PaginationState(int defaultPerPage, bool showTotal = true)
    {
        ShowTotal = showTotal;
        PerPage = DefaultPerPage = defaultPerPage;
    }

    public void Init(int? pageIndex, int? perPage, Dictionary<string, string?> allRouteData)
    {
        if (pageIndex != null)
            PageIndex = pageIndex.Value;

        if (perPage != null)
            PerPage = perPage.Value;

        AllRouteData = allRouteData;
        if (PerPage != DefaultPerPage)
            AllRouteData.Add("perPage", PerPage.ToString(CultureInfo.InvariantCulture));
    }

    public async Task LoadAsync(IQueryable<T> query, int? count = null)
    {
        List = await PaginatedList<T>.CreateAsync(query, PageIndex, PerPage, count, ShowTotal);
    }

    public async Task LoadLinqAsync<TQuery>(
        IQueryable<TQuery> query,
        Func<IEnumerable<TQuery>, IEnumerable<T>> convert
    )
    {
        List = await PaginatedList<T>.CreateLinqAsync(query, convert, PageIndex, PerPage, showTotal: ShowTotal);
    }
}

public sealed record PaginatedList<T>(T[] PaginatedItems, int? TotalCount, int PageIndex, int PageSize)
{
    public bool HasNextPage => PaginatedItems.Length >= PageSize;
    public bool HasPrevPage => PageIndex > 0;

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> query,
        int pageIndex,
        int pageSize,
        int? count = null,
        bool showTotal = true
    ) => await CreateLinqAsync(query, e => e, pageIndex, pageSize, count, showTotal);

    public static async Task<PaginatedList<T>> CreateLinqAsync<TQuery>(
        IQueryable<TQuery> query,
        Func<IEnumerable<TQuery>, IEnumerable<T>> convert,
        int pageIndex,
        int pageSize,
        int? count = null,
        bool showTotal = true
    ) => new(
        convert(await query.Skip(pageIndex * pageSize).Take(pageSize).ToArrayAsync()).ToArray(),
        showTotal ? count ?? await query.CountAsync() : null,
        pageIndex,
        pageSize
    );
}
