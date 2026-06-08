using System.Runtime.InteropServices;

namespace MiniDB.Core.Types;

[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct DbValue : IEquatable<DbValue>
{
    [FieldOffset(0)] private readonly long _i64;
    [FieldOffset(0)] private readonly double _f64;
    [FieldOffset(0)] private readonly ulong _raw;

    private DbValue(long value) { _raw = 0; _f64 = 0; _i64 = value; }
    private DbValue(double value) { _raw = 0; _i64 = 0; _f64 = value; }
    private DbValue(ulong value) { _i64 = 0; _f64 = 0; _raw = value; }

    public static DbValue FromInt64(long value) => new(value);
    public static DbValue FromFloat64(double value) => new(value);
    public static DbValue FromBool(bool value) => new(value ? 1L : 0L);
    public static DbValue FromTextRef(uint offset, uint length)
    {
        ulong packed = ((ulong)offset << 32) | length;
        return new DbValue(packed);
    }

    public static readonly DbValue Null = default;

    public long AsInt64() => _i64;
    public double AsFloat64() => _f64;
    public bool AsBool() => _i64 != 0;
    public ulong RawBits() => _raw;

    public (uint offset, uint length) AsTextRef()
    {
        var offset = (uint)(_raw >> 32);
        var length = (uint)(_raw & 0xFFFFFFFF);
        return (offset, length);
    }

    public bool Equals(DbValue other) => _raw == other._raw;
    public override bool Equals(object? obj) => obj is DbValue other && Equals(other);
    public override int GetHashCode() => _raw.GetHashCode();
    public static bool operator ==(DbValue left, DbValue right) => left.Equals(right);
    public static bool operator !=(DbValue left, DbValue right) => !left.Equals(right);

    public override string ToString() =>
        $"DbValue(0x{_raw:X16})";
}