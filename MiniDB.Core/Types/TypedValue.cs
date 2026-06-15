using System.Runtime.InteropServices;

namespace MiniDB.Core.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct TypedValue(DbType Type, DbValue Value)
{
    public string? ResolvedText { get; private init; }
    public bool IsNull => Type == DbType.Null;

    public static TypedValue Null => new TypedValue(DbType.Null, DbValue.Null);
    public static TypedValue Of(long value) => new TypedValue(DbType.Int64, DbValue.FromInt64(value));
    public static TypedValue Of(double value) => new TypedValue(DbType.Float64, DbValue.FromFloat64(value));
    public static TypedValue Of(bool value) => new TypedValue(DbType.Bool, DbValue.FromBool(value));
    public static TypedValue TextRef(uint offset, uint length) =>
        new TypedValue(DbType.Text, DbValue.FromTextRef(offset, length));
    public static TypedValue FromString(string value) =>
        new(DbType.Text, DbValue.FromInt64(0L)) { ResolvedText = value };

    public override string ToString() => Type switch
    {
        DbType.Null => "NULL",
        DbType.Int64 => Value.AsInt64().ToString(),
        DbType.Float64 => Value.AsFloat64().ToString(),
        DbType.Bool => Value.AsBool().ToString(),
        DbType.Text => $"TEXT@{Value.AsTextRef()}",
        _ => "?"
    };
}