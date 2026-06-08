using MiniDB.Core.Exceptions;

namespace MiniDB.Core.Types;

public static class DbValueOps
{
    public static int Compare(TypedValue a, TypedValue b)
    {
        if (a.IsNull && b.IsNull) return 0;
        if (a.IsNull) return -1;
        if (b.IsNull) return 1;

        return (a.Type, b.Type) switch
        {
            (DbType.Int64, DbType.Int64) => a.Value.AsInt64().CompareTo(b.Value.AsInt64()),
            (DbType.Float64, DbType.Float64) => a.Value.AsFloat64().CompareTo(b.Value.AsFloat64()),
            (DbType.Int64, DbType.Float64) => ((double)a.Value.AsInt64()).CompareTo(b.Value.AsFloat64()),
            (DbType.Float64, DbType.Int64) => a.Value.AsFloat64().CompareTo(b.Value.AsInt64()),
            (DbType.Bool, DbType.Bool) => a.Value.AsBool().CompareTo(b.Value.AsBool()),
            (DbType.Text, DbType.Text) => string.CompareOrdinal(
                a.Value.AsTextRef().ToString(), b.Value.AsTextRef().ToString()),
            _ => throw new InvalidOperationException($"Cannot compare {a.Type} and {b.Type}")
        };
    }

    public static TypedValue Add(TypedValue a, TypedValue b)
    {
        if (a.IsNull || b.IsNull) return TypedValue.Null;

        return (a.Type, b.Type) switch
        {
            (DbType.Int64, DbType.Int64) => TypedValue.Of(a.Value.AsInt64() + b.Value.AsInt64()),
            (DbType.Float64, DbType.Float64) => TypedValue.Of(a.Value.AsFloat64() + b.Value.AsFloat64()),
            (DbType.Int64, DbType.Float64) => TypedValue.Of(a.Value.AsInt64() + b.Value.AsFloat64()),
            (DbType.Float64, DbType.Int64) => TypedValue.Of(a.Value.AsFloat64() + b.Value.AsInt64()),
            _ => throw new InvalidOperationException($"Cannot add {a.Type} and {b.Type}")
        };
    }

    public static TypedValue Subtract(TypedValue a, TypedValue b)
    {
        if (a.IsNull || b.IsNull) return TypedValue.Null;

        return (a.Type, b.Type) switch
        {
            (DbType.Int64, DbType.Int64) => TypedValue.Of(a.Value.AsInt64() - b.Value.AsInt64()),
            (DbType.Float64, DbType.Float64) => TypedValue.Of(a.Value.AsFloat64() - b.Value.AsFloat64()),
            (DbType.Int64, DbType.Float64) => TypedValue.Of(a.Value.AsInt64() - b.Value.AsFloat64()),
            (DbType.Float64, DbType.Int64) => TypedValue.Of(a.Value.AsFloat64() - b.Value.AsInt64()),
            _ => throw new InvalidOperationException($"Cannot subtract {a.Type} and {b.Type}")
        };
    }

    public static TypedValue Multiply(TypedValue a, TypedValue b)
    {
        if (a.IsNull || b.IsNull) return TypedValue.Null;

        return (a.Type, b.Type) switch
        {
            (DbType.Int64, DbType.Int64) => TypedValue.Of(a.Value.AsInt64() * b.Value.AsInt64()),
            (DbType.Float64, DbType.Float64) => TypedValue.Of(a.Value.AsFloat64() * b.Value.AsFloat64()),
            (DbType.Int64, DbType.Float64) => TypedValue.Of(a.Value.AsInt64() * b.Value.AsFloat64()),
            (DbType.Float64, DbType.Int64) => TypedValue.Of(a.Value.AsFloat64() * b.Value.AsInt64()),
            _ => throw new InvalidOperationException($"Cannot multiply {a.Type} and {b.Type}")
        };
    }

    public static TypedValue Divide(TypedValue a, TypedValue b)
    {
        if (a.IsNull || b.IsNull) return TypedValue.Null;

        bool divisorIs = b.Type switch
        {
            DbType.Int64 => b.Value.AsInt64() == 0,
            DbType.Float64 => b.Value.AsFloat64() == 0.0,
            _ => false
        };

        if (divisorIs) throw new DivisionByZeroException($"Division by zero: {a} / {b}");

        return (a.Type, b.Type) switch
        {
            (DbType.Int64, DbType.Int64) => TypedValue.Of(a.Value.AsInt64() / b.Value.AsInt64()),
            (DbType.Float64, DbType.Float64) => TypedValue.Of(a.Value.AsFloat64() / b.Value.AsFloat64()),
            (DbType.Int64, DbType.Float64) => TypedValue.Of(a.Value.AsInt64() / b.Value.AsFloat64()),
            (DbType.Float64, DbType.Int64) => TypedValue.Of(a.Value.AsFloat64() / b.Value.AsInt64()),
            _ => throw new InvalidOperationException($"Cannot divide {a.Type} and {b.Type}")
        };
    }

    public static TypedValue Modulo(TypedValue a, TypedValue b)
    {
        if (a.IsNull || b.IsNull) return TypedValue.Null;

        if (a.Type != DbType.Int64 || b.Type != DbType.Int64)
            throw new InvalidOperationException(
                $"Modulo is only defined for Int64, got {a.Type} % {b.Type}");

        if (b.Value.AsInt64() == 0L)
            throw new DivisionByZeroException(
                $"Modulo by zero: {a} % {b}");

        return TypedValue.Of(a.Value.AsInt64() % b.Value.AsInt64());
    }
}