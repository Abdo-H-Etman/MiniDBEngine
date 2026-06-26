namespace MiniDB.Core.Sql.Ast;

public sealed class ColumnExpr
{
    public Expr Expression { get; }
    public string? Alias { get; }
    public bool IsStar { get; }

    public ColumnExpr(Expr expression, string? alias, bool isStar)
    {
        Expression = expression;
        Alias = alias;
        IsStar = isStar;
    }

    public static ColumnExpr Star() =>
        new(new ColumnRefExpr("*"), null, isStar: true);

    public static ColumnExpr Named(Expr expr, string? alias = null) =>
        new(expr, alias, isStar: false);

    public string OutputName => Alias ??
        (Expression is ColumnRefExpr col ? col.ColumnName : "?");

    public override string ToString() =>
        IsStar ? "*" :
        Alias is null ? Expression.ToString()! :
        $"{Expression} AS {Alias}";
}