namespace MiniDB.Core.Types;

public sealed class ResolvedValue
{
    public DbType Type { get; }
    public DbValue Value { get; }
    public string? ResolvedText { get; }

    public bool IsNull => Type == DbType.Null;

    private ResolvedValue(DbType type, DbValue value, string? text)
    {
        Type = type;
        Value = value;
        ResolvedText = text;
    }

    public static ResolvedValue FromTyped(TypedValue tv) =>
        new(tv.Type, tv.Value, null);

    public static ResolvedValue FromString(string text) =>
        new(DbType.Text, DbValue.FromInt64(0L), text);

    public static ResolvedValue Null =>
        new(DbType.Null, DbValue.Null, null);


    public override string ToString() => Type switch
    {
        DbType.Null => "NULL",
        DbType.Int64 => Value.AsInt64().ToString(),
        DbType.Float64 => Value.AsFloat64().ToString("G"),
        DbType.Bool => Value.AsBool() ? "TRUE" : "FALSE",
        DbType.Text => ResolvedText ?? $"TEXT@{Value.AsTextRef()}",
        _ => "?"
    };
}