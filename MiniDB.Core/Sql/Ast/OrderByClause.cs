namespace MiniDB.Core.Sql.Ast;

public enum SortDirection
{
    Asc,
    Desc
}
public sealed class OrderByClause
{
    public Expr Expression { get; }
    public SortDirection Direction { get; }

    public OrderByClause(Expr expression, SortDirection direction = SortDirection.Asc)
    {
        Expression = expression;
        Direction = direction;
    }

    public override string ToString() =>
        $"{Expression} {Direction.ToString().ToUpper()}";
}