using MiniDB.Core.Types;

namespace MiniDB.Core.Sql.Ast;

public abstract class Expr { }

public sealed class LiteralExpr : Expr
{
    public DbType Type { get; }
    public object? Value { get; }

    private LiteralExpr(DbType type, object? value)
    {
        Type = type;
        Value = value;
    }

    public static LiteralExpr Integer(long value) => new(DbType.Int64, value);
    public static LiteralExpr Float(double value) => new(DbType.Float64, value);
    public static LiteralExpr String(string value) => new(DbType.Text, value);
    public static LiteralExpr Bool(bool value) => new(DbType.Bool, value);
    public static LiteralExpr Null() => new(DbType.Null, null);

    public override string ToString() => Value?.ToString() ?? "NULL";
}

public sealed class ColumnRefExpr : Expr
{
    public string ColumnName { get; }
    public string? TableName { get; }
    public ColumnRefExpr(string columnName, string? tableName = null)
    {
        ColumnName = columnName;
        TableName = tableName;
    }

    public string FullName => TableName is null ?
                        ColumnName :
                        $"{TableName}.{ColumnName}";

    public override string ToString() => FullName;
}

public enum BinaryOp
{
    Add, Subtract, Multiply, Divide, Modulo,
    Eq, NotEq, Gt, GtEq, Lt, LtEq,
    And, Or,
    Like,
    Between
}

public sealed class BinaryExpr : Expr
{
    public Expr Left { get; }
    public BinaryOp Op { get; }
    public Expr Right { get; }

    public BinaryExpr(Expr left, BinaryOp op, Expr right)
    {
        Left = left;
        Op = op;
        Right = right;
    }

    public override string ToString() => $"{Left} {Op} {Right}";
}

public enum UnaryOp
{
    Negate,
    Not
}

public sealed class UnaryExpr : Expr
{
    public UnaryOp Op { get; }
    public Expr Operand { get; }

    public UnaryExpr(UnaryOp op, Expr operand)
    {
        Op = op;
        Operand = operand;
    }

    public override string ToString() => $"{Op} {Operand}";
}

public sealed class IsNullExpr : Expr
{
    public Expr Operand { get; }
    public bool Negated { get; }

    public IsNullExpr(Expr operand, bool negated = false)
    {
        Operand = operand;
        Negated = negated;
    }

    public override string ToString() =>
        Negated ? $"{Operand} IS NOT NULL" : $"{Operand} IS NULL";
}

public sealed class InExpr : Expr
{
    public Expr Operand { get; }
    public Expr[] Values { get; }
    public bool Negated { get; }

    public InExpr(Expr operand, Expr[] values, bool negated = false)
    {
        Operand = operand;
        Values = values;
        Negated = negated;
    }

    public override string ToString() =>
        $"({Operand} {(Negated ? "NOT IN" : "IN")} ({string.Join(", ", Values.AsEnumerable())}))";
}

public sealed class BetweenExpr : Expr
{
    public Expr Operand { get; }
    public Expr Low { get; }
    public Expr High { get; }
    public bool Negated { get; }

    public BetweenExpr(Expr operand, Expr low, Expr high, bool negated = false)
    {
        Operand = operand;
        Low = low;
        High = high;
        Negated = negated;
    }

    public override string ToString() =>
        $"({Operand} {(Negated ? "NOT BETWEEN" : "BETWEEN")} {Low} AND {High})";
}