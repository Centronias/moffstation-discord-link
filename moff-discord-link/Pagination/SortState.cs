using System.Collections.Immutable;
using System.Linq.Expressions;

namespace MoffDiscordLink.Pagination;

public interface ISortState
{
    string OrderStringForColumnButton(string column);
    IDictionary<string, string?> AllRouteData { get; }
}

public sealed class SortState<T> : ISortState
{
    public string? CurColumn { get; private set; }
    public string? DefaultColumn { get; private set; }
    public SortOrder CurOrder { get; private set; } = SortOrder.Ascending;
    private readonly Dictionary<string, ColumnReg> _columns = new();

    public void Init(string? reqOrder, IDictionary<string, string?> allRouteData)
    {
        if (DefaultColumn == null)
            throw new InvalidOperationException("No default column set!");

        var originalReqOrder = reqOrder;

        if (reqOrder != null)
        {
            if (reqOrder.EndsWith("_desc"))
            {
                CurOrder = SortOrder.Descending;
                reqOrder = reqOrder[..^5];
            }

            CurColumn = reqOrder;
        }
        else
        {
            CurColumn = DefaultColumn;
            CurOrder = _columns[DefaultColumn].SortDefault!.Value;
        }

        AllRouteData = allRouteData;
        allRouteData.Add("sort", originalReqOrder);
    }

    public string OrderStringForColumnButton(string column)
    {
        var colReg = _columns[column];
        // If current column, flip order around.
        if (column == CurColumn)
            return StringWithOrder(Flip(CurOrder));

        // If default column, select default column order.
        // Otherwise ascending order.
        return StringWithOrder(colReg.SortDefault is { } def ? def : SortOrder.Ascending);

        string StringWithOrder(SortOrder order)
        {
            // If default order return "" for string so URL is cleaner.
            if (colReg.SortDefault != null && colReg.SortDefault == order)
                return "";

            return order == SortOrder.Descending ? $"{column}_desc" : column;
        }
    }

    private static SortOrder Flip(SortOrder order)
    {
        return order switch
        {
            SortOrder.Ascending => SortOrder.Descending,
            _ => SortOrder.Ascending
        };
    }

    public IDictionary<string, string?> AllRouteData { get; private set; } = ImmutableDictionary<string, string?>.Empty;

    public void AddColumn<TKey>(string name, Expression<Func<T, TKey>> expr, SortOrder? sortDefault = null)
    {
        AddColumn(name, expr, expr, sortDefault);
    }

    /// <summary>
    /// Add a column that can be sorted.
    /// </summary>
    /// <param name="name">The ID of the column, used in the HTML.</param>
    /// <param name="exprDesc">Expression to use as sort key when sorting descending.</param>
    /// <param name="exprAsc">Expression to use as sort key when sorting ascending.</param>
    /// <param name="sortDefault">If given, this column is the default sort column, with the given order.</param>
    /// <typeparam name="TKey">The type of items to use as sort key.</typeparam>
    public void AddColumn<TKey>(
        string name,
        Expression<Func<T, TKey>> exprDesc,
        Expression<Func<T, TKey>> exprAsc,
        SortOrder? sortDefault = null)
    {
        _columns.Add(name, new ColumnReg<TKey>(exprAsc, exprDesc, sortDefault));

        if (sortDefault != null)
            DefaultColumn = name;
    }

    public IOrderedQueryable<T> ApplyToQuery(IQueryable<T> query)
    {
        var col = _columns[CurColumn!];
        return col.ApplyToQuery(query, CurOrder);
    }

    private abstract record ColumnReg(SortOrder? SortDefault)
    {
        public abstract IOrderedQueryable<T> ApplyToQuery(IQueryable<T> query, SortOrder order);
    }

    private sealed record ColumnReg<TKey>(
        Expression<Func<T, TKey>> ExprAscending,
        Expression<Func<T, TKey>> ExprDescending,
        SortOrder? SortDefault)
        : ColumnReg(SortDefault)
    {
        public override IOrderedQueryable<T> ApplyToQuery(IQueryable<T> query, SortOrder order)
        {
            return order switch
            {
                SortOrder.Ascending => query.OrderBy(ExprAscending),
                SortOrder.Descending => query.OrderByDescending(ExprDescending),
                _ => throw new InvalidOperationException()
            };
        }
    }
}

public enum SortOrder
{
    Ascending,
    Descending
}
