using System.Text;
using FluentAssertions;
using MiniDB.Core.Storage;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Storage;

public class RowSerializerTests
{
    // ── CalculateSize ─────────────────────────────────────────────────────────
    [Fact]
    public void CalculateSize_ThreeColumns_NoText() =>
        RowSerializer.CalculateSize(3, 0).Should().Be(27);

    [Fact]
    public void CalculateSize_OneColumn_WithText() =>
        RowSerializer.CalculateSize(1, 5).Should().Be(14);

    // ── Serialize / Deserialize round-trips ───────────────────────────────────
    [Fact]
    public void RoundTrip_Int64Row()
    {
        TypedValue[] row = [TypedValue.Of(42L), TypedValue.Of(100L)];
        int size = RowSerializer.CalculateSize(row.Length, 0);
        byte[] buff = new byte[size];

        RowSerializer.Serialize(row, null, buff);
        var result = RowSerializer.Deserialize(buff, row.Length, pageBase: 0);

        result[0].Type.Should().Be(DbType.Int64);
        result[0].Value.AsInt64().Should().Be(42L);
        result[1].Type.Should().Be(DbType.Int64);
        result[1].Value.AsInt64().Should().Be(100L);
    }

    [Fact]
    public void RoundTrip_Float64Row()
    {
        TypedValue[] row = [TypedValue.Of(3.14)];
        int size = RowSerializer.CalculateSize(row.Length, 0);
        byte[] buff = new byte[size];

        RowSerializer.Serialize(row, null, buff);
        var result = RowSerializer.Deserialize(buff, row.Length, pageBase: 0);

        result[0].Type.Should().Be(DbType.Float64);
        result[0].Value.AsFloat64().Should().Be(3.14);
    }

    [Fact]
    public void RoundTrip_BoolRow()
    {
        TypedValue[] row = [TypedValue.Of(true), TypedValue.Of(false)];
        int size = RowSerializer.CalculateSize(row.Length, 0);
        byte[] buff = new byte[size];

        RowSerializer.Serialize(row, null, buff);
        var result = RowSerializer.Deserialize(buff, row.Length, pageBase: 0);

        result[0].Value.AsBool().Should().BeTrue();
        result[1].Value.AsBool().Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_NullRow()
    {
        TypedValue[] row = [TypedValue.Null, TypedValue.Of(7L)];
        int size = RowSerializer.CalculateSize(row.Length, 0);
        byte[] buf = new byte[size];

        RowSerializer.Serialize(row, null, buf);
        var result = RowSerializer.Deserialize(buf, row.Length, pageBase: 0);

        result[0].IsNull.Should().BeTrue();
        result[1].Value.AsInt64().Should().Be(7L);
    }

    [Fact]
    public void RoundTrip_MixedTypes()
    {
        TypedValue[] row =
        [
            TypedValue.Of(1L),
            TypedValue.Of(2.5),
            TypedValue.Of(true),
            TypedValue.Null
        ];
        int size = RowSerializer.CalculateSize(row.Length, 0);
        byte[] buf = new byte[size];

        RowSerializer.Serialize(row, null, buf);
        var result = RowSerializer.Deserialize(buf, row.Length, pageBase: 0);

        result[0].Type.Should().Be(DbType.Int64);
        result[1].Type.Should().Be(DbType.Float64);
        result[2].Type.Should().Be(DbType.Bool);
        result[3].Type.Should().Be(DbType.Null);
    }

    [Fact]
    public void TextColumn_RefIsPageRelative()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("hello");
        TypedValue[] row = [TypedValue.TextRef(0, 0)]; // placeholder
        int size = RowSerializer.CalculateSize(1, textBytes.Length);
        byte[] buf = new byte[size];

        // Manually create a text-typed row for serialization
        var textRow = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new[] { textBytes };

        RowSerializer.Serialize(textRow, texts, buf);
        var result = RowSerializer.Deserialize(buf, 1, pageBase: 3000);

        result[0].Type.Should().Be(DbType.Text);
        var (offset, length) = result[0].Value.AsTextRef();
        length.Should().Be(5u);
        offset.Should().Be(3009u);
    }
}