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
    public IDictionary<string, string?> AllRouteData { get; private set; } = ImmutableDictionary<string, string?>.Empty;

    private readonly Dictionary<string, ColumnReg> _columns = new();

    public void Init(string? reqOrder, IDictionary<string, string?> allRouteData)
    {
        if (DefaultColumn == null)
            throw new InvalidOperationException("No default column set.");

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

        if (column == CurColumn)
            return StringWithOrder(Flip(CurOrder));

        return StringWithOrder(colReg.SortDefault ?? SortOrder.Ascending);

        string StringWithOrder(SortOrder order)
        {
            // Return "" for the default sort so the URL stays clean.
            if (colReg.SortDefault != null && colReg.SortDefault == order)
                return "";

            return order == SortOrder.Descending ? $"{column}_desc" : column;
        }
    }

    public void AddColumn<TKey>(string name, Expression<Func<T, TKey>> expr, SortOrder? sortDefault = null)
    {
        AddColumn(name, expr, expr, sortDefault);
    }

    public void AddColumn<TKey>(
        string name,
        Expression<Func<T, TKey>> exprAsc,
        Expression<Func<T, TKey>> exprDesc,
        SortOrder? sortDefault = null)
    {
        _columns.Add(name, new ColumnReg<TKey>(exprAsc, exprDesc, sortDefault));

        if (sortDefault != null)
            DefaultColumn = name;
    }

    public IOrderedQueryable<T> ApplyToQuery(IQueryable<T> query)
    {
        return _columns[CurColumn!].ApplyToQuery(query, CurOrder);
    }

    private static SortOrder Flip(SortOrder order) =>
        order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

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
        public override IOrderedQueryable<T> ApplyToQuery(IQueryable<T> query, SortOrder order) => order switch
        {
            SortOrder.Ascending => query.OrderBy(ExprAscending),
            SortOrder.Descending => query.OrderByDescending(ExprDescending),
            _ => throw new InvalidOperationException()
        };
    }
}

public enum SortOrder
{
    Ascending,
    Descending,
}
