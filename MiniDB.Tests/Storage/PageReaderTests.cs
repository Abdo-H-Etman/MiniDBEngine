using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using MiniDB.Core.Storage;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Storage;

public class PageReaderTests : IDisposable
{
    private readonly RawPage _page = RawPage.Allocate(1);
    private readonly PageWriter _writer;
    private readonly PageReader _reader;

    public PageReaderTests()
    {
        _writer = new PageWriter(_page);
        _reader = new PageReader(_page);
    }

    public void Dispose() => _page.Dispose();

    // ── basic reads ───────────────────────────────────────────────────────────

    [Fact]
    public void ReadRow_SingleInt64_RoundTrips()
    {
        TypedValue[] row = [TypedValue.Of(50L)];
        _writer.TryInsert(row);

        var result = _reader.ReadRow(0, 1);

        result[0].Type.Should().Be(DbType.Int64);
        result[0].Value.AsInt64().Should().Be(50L);
    }

    [Fact]
    public void ReadRow_SingleFloat64_RoundTrips()
    {
        TypedValue[] row = [TypedValue.Of(5.5)];
        _writer.TryInsert(row);

        var result = _reader.ReadRow(0, 1);

        result[0].Type.Should().Be(DbType.Float64);
        result[0].Value.AsFloat64().Should().Be(5.5);
    }

    [Fact]
    public void ReadRow_Bool_RoundTrips()
    {
        _writer.TryInsert([TypedValue.Of(true)]);
        _writer.TryInsert([TypedValue.Of(false)]);

        var row1 = _reader.ReadRow(0, 1);
        row1[0].Value.AsBool().Should().BeTrue();
        var row2 = _reader.ReadRow(1, 1);
        row2[0].Value.AsBool().Should().BeFalse();
    }

    [Fact]
    public void ReadRow_Null_RoundTrips()
    {
        _writer.TryInsert([TypedValue.Null]);

        var result = _reader.ReadRow(0, 1);

        result[0].IsNull.Should().BeTrue();
    }

    [Fact]
    public void ReadRow_MixedTypes_RoundTrips()
    {
        TypedValue[] row =
        [
            TypedValue.Of(99L),
            TypedValue.Of(1.23),
            TypedValue.Of(true),
            TypedValue.Null
        ];
        _writer.TryInsert(row);

        var result = _reader.ReadRow(0, columnCount: 4);
        result[0].Type.Should().Be(DbType.Int64);
        result[0].Value.AsInt64().Should().Be(99L);
        result[1].Type.Should().Be(DbType.Float64);
        result[1].Value.AsFloat64().Should().Be(1.23);
        result[2].Type.Should().Be(DbType.Bool);
        result[2].Value.AsBool().Should().BeTrue();
        result[3].Type.Should().Be(DbType.Null);
    }

    // ── text columns ──────────────────────────────────────────────────────────

    [Fact]
    public void ReadRow_Text_ResolvedToString()
    {
        var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new string?[] { "hello" };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 1);

        result[0].Type.Should().Be(DbType.Text);
        result[0].ResolvedText.Should().Be("hello");
    }

    [Fact]
    public void ReadRow_EmptyString_ResolvedCorrectly()
    {
        var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new string?[] { "" };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 1);

        result[0].Type.Should().Be(DbType.Text);
        result[0].ResolvedText.Should().Be(string.Empty);
    }

    [Fact]
    public void ReadRow_LongText_ResolvedCorrectly()
    {
        string longText = new('X', 500);
        var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new string?[] { longText };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 1);

        result[0].ResolvedText.Should().Be(longText);
    }

    [Fact]
    public void ReadRow_UniCodeText_ResolvedCorrectly()
    {
        string arabic = "مرحبا";
        var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new string?[] { arabic };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 1);

        result[0].ResolvedText.Should().Be(arabic);
    }

    [Fact]
    public void ReadRow_MixedTextAndInt_BothResolve()
    {
        var row = new TypedValue[] {
            TypedValue.Of(10L),
            new(DbType.Text, DbValue.Null)};
        var texts = new string?[] { null, "hello" };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 2);

        result[0].Value.AsInt64().Should().Be(10L);
        result[1].ResolvedText.Should().Be("hello");
    }

    // ── multiple rows ─────────────────────────────────────────────────────────

    [Fact]
    public void ReadRow_MultipleRows_EachCorrect()
    {
        for (int i = 0; i < 10; i++)
            _writer.TryInsert([TypedValue.Of(i)]);

        for (int i = 0; i < 10; i++)
        {
            var result = _reader.ReadRow(i, columnCount: 1);
            Assert.Equal(i, result[0].Value.AsInt64());
        }
    }

    // ── deleted slots ─────────────────────────────────────────────────────────

    [Fact]
    public void ReadRow_Deleted_Throws()
    {
        _writer.TryInsert([TypedValue.Of(1L)]);
        _writer.Delete(0);

        var act = () => _reader.ReadRow(0, 1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryReadRow_Deleted_ReturnsNull()
    {
        _writer.TryInsert([TypedValue.Of(1L)]);
        _writer.Delete(0);

        var result = _reader.TryReadRow(0, 1);
        result.Should().BeNull();
    }

    [Fact]
    public void TryReadRow_OutOfRange_ReturnsNull() =>
        _reader.TryReadRow(99, columnCount: 1).Should().BeNull();

    // ── ReadAllRows ───────────────────────────────────────────────────────────

    [Fact]
    public void ReadAllRows_SkipsDeletedSlots()
    {
        _writer.TryInsert([TypedValue.Of(1L)]);
        _writer.TryInsert([TypedValue.Of(2L)]);
        _writer.TryInsert([TypedValue.Of(3L)]);
        _writer.Delete(1);

        var rows = _reader.ReadAllRows(columnCount: 1).ToList();

        rows.Count.Should().Be(2);
        rows[0].row[0].Value.AsInt64().Should().Be(1L);
        rows[1].row[0].Value.AsInt64().Should().Be(3L);
    }

    [Fact]
    public void ReadAllRows_EmptyPage_ReturnsEmpty()
    {
        var rows = _reader.ReadAllRows(columnCount: 1).ToList();
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadAllRows_SlotIndexsAreCorrect()
    {
        _writer.TryInsert([TypedValue.Of(1L)]);
        _writer.TryInsert([TypedValue.Of(2L)]);
        _writer.Delete(0);
        _writer.TryInsert([TypedValue.Of(3L)]);

        var rows = _reader.ReadAllRows(columnCount: 1).ToList();

        rows.Count.Should().Be(2);
        rows[0].slotIndex.Should().Be(1);
        rows[1].slotIndex.Should().Be(2);
    }

    // ── ReadWhere ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadWhere_FilterCorrecly()
    {
        for (int i = 0; i < 10; i++)
            _writer.TryInsert([TypedValue.Of(i)]);

        var rows = _reader.ReadWhere(1, row => row[0].Value.AsInt64() > 5)
                    .ToList();

        rows.Count.Should().Be(4);
        rows.Should().AllSatisfy(r => r.row[0].Value.AsInt64().Should().BeGreaterThan(5));
    }

    // ── metadata ──────────────────────────────────────────────────────────────

    [Fact]
    public void SlotCount_ReflectsInserts()
    {
        _reader.SlotCount.Should().Be(0);
        _writer.TryInsert([TypedValue.Of(10L)]);
        _reader.SlotCount.Should().Be(1);
        _writer.TryInsert([TypedValue.Of(20L)]);
        _reader.SlotCount.Should().Be(2);
    }

    [Fact]
    public void LiveRowCount_ExcludesDeleted()
    {
        _writer.TryInsert([TypedValue.Of(5L)]);
        _writer.TryInsert([TypedValue.Of(2L)]);
        _writer.TryInsert([TypedValue.Of(4L)]);
        _writer.Delete(1);
        _reader.LiveRowCount().Should().Be(2);
    }

    [Fact]
    public void FreeBytes_DecreasesAfterInsert()
    {
        int befor = _reader.FreeBytes;
        _writer.TryInsert([TypedValue.Of(10.0)]);
        int after = _reader.FreeBytes;

        (befor > after).Should().BeTrue();
    }

    // ── error cases ───────────────────────────────────────────────────────────

    [Fact]
    public void ReadRow_OutOfRange_Trows()
    {
        var act = () => _reader.ReadRow(0, 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadRow_ZeroColumnCount_Throws()
    {
        _writer.TryInsert([TypedValue.Of(10L)]);
        var act = () => _reader.ReadRow(0, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

}