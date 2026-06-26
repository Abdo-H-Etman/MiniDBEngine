namespace MiniDB.Core.Sql.Ast;

public abstract class Statement { }

public sealed class SelectStatement : Statement
{
    public ColumnExpr[] Columns { get; }
    public string From { get; }
    public Expr? Where { get; }
    public OrderByClause[] OrderBy { get; }
    public int? Limit { get; }
    public int? Offset { get; }
    public SelectStatement(
        ColumnExpr[] columns,
        string from,
        Expr? where = null,
        OrderByClause[]? orderBy = null,
        int? limit = null,
        int? offset = null)
    {
        if (columns.Length == 0)
            throw new ArgumentException(
                "SELECT must have at least one column.", nameof(columns));
        Columns = columns;
        From = from;
        Where = where;
        OrderBy = orderBy ?? [];
        Limit = limit;
        Offset = offset;
    }

    public bool HasWhere => Where is not null;
    public bool HasOrderBy => OrderBy.Length > 0;
    public bool HasLimit => Limit is not null;
    public bool HasOffset => Offset is not null;
    public bool IsSelectStar => Columns.Length == 1 && Columns[0].IsStar;

    public override string ToString()
    {
        var cols = string.Join(", ", Columns.Select(c => c.ToString()));
        var sb = $"SELECT {cols} FROM {From}";
        if (HasWhere) sb += $" WHERE {Where}";
        if (HasOrderBy) sb += $" ORDER BY {string.Join(", ", OrderBy.Select(o => o.ToString()))}";
        if (HasLimit) sb += $" LIMIT {Limit}";
        if (HasOffset) sb += $" OFFSET {Offset}";

        return sb;
    }
}

public sealed class InsertStatement : Statement
{
    public string Table { get; }
    public string[] Columns { get; }
    public LiteralExpr[][] Rows { get; }

    public InsertStatement(string table, string[] columns, LiteralExpr[][] rows)
    {
        if (rows.Length == 0)
            throw new ArgumentException(
                "INSERT must have at least one row of values.", nameof(rows));

        Table = table;
        Columns = columns;
        Rows = rows;
    }

    public bool HasColumnList => Columns.Length > 0;
    public int RowCount => Rows.Length;

    public override string ToString()
    {
        var cols = HasColumnList ?
            $" ({string.Join(", ", Columns)})" :
            string.Empty;

        var values = string.Join(", ",
            Rows.Select(r =>
                $"({string.Join(", ", r.Select(v => v.ToString()))})"));

        return $"INSERT INTO {Table}{cols} VALUES {values}";
    }
}