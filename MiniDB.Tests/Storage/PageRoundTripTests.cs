using FluentAssertions;
using MiniDB.Core.Storage;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Storage;

public class PageRoundTripTests : IDisposable
{
    private readonly RawPage _page;
    private readonly PageWriter _writer;
    private readonly PageReader _reader;

    public PageRoundTripTests()
    {
        _page = RawPage.Allocate(1);
        _writer = new PageWriter(_page);
        _reader = new PageReader(_page);
    }

    public void Dispose() => _page.Dispose();

    [Fact]
    public void Insert50Rows_ReadAllBack_AllMatch()
    {
        var inserted = new List<TypedValue[]>();

        for (int i = 0; i < 50; i++)
        {
            TypedValue[] row =
            [
                TypedValue.Of(i),
                TypedValue.Of(i * 1.5),
                TypedValue.Of(i % 2 == 0)
            ];
            inserted.Add(row);

            int slot = _writer.TryInsert(row);

            slot.Should().BeGreaterThanOrEqualTo(0, $"Insert {i} failed");
        }

        var readed = _reader.ReadAllRows(3).ToList();

        readed.Count.Should().Be(50);

        for (int i = 0; i < 50; i++)
        {
            var original = inserted[i];
            var readBack = readed[i].row;

            original[0].Value.AsInt64().Should().Be(readBack[0].Value.AsInt64());
            original[1].Value.AsInt64().Should().Be(readBack[1].Value.AsInt64());
            original[2].Value.AsInt64().Should().Be(readBack[2].Value.AsInt64());
        }
    }

    [Fact]
    public void NullColumns_SurviveRoundTrip()
    {
        TypedValue[] row =
        [
            TypedValue.Null,
            TypedValue.Of(40L),
            TypedValue.Null
        ];

        _writer.TryInsert(row);

        var result = _reader.ReadRow(0, 3);

        result[0].IsNull.Should().BeTrue();
        result[1].Value.AsInt64().Should().Be(40L);
        result[2].IsNull.Should().BeTrue();
    }

    [Fact]
    public void TextColumns_SurviveRoundTrip()
    {
        var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
        var texts = new string[] { "MiniDb" };
        _writer.TryInsert(row, texts);

        var result = _reader.ReadRow(0, 1);
        result[0].ResolvedText.Should().Be("MiniDb");
    }

    [Fact]
    public void ExtremeInt64Values_SurviveRoundTrip()
    {
        _writer.TryInsert([TypedValue.Of(long.MaxValue)]);
        _writer.TryInsert([TypedValue.Of(long.MinValue)]);
        _writer.TryInsert([TypedValue.Of(0L)]);

        var row1 = _reader.ReadRow(0, 1);
        row1[0].Value.AsInt64().Should().Be(long.MaxValue);
        var row2 = _reader.ReadRow(1, 1);
        row2[0].Value.AsInt64().Should().Be(long.MinValue);
        var row3 = _reader.ReadRow(2, 1);
        row3[0].Value.AsInt64().Should().Be(0L);
    }
    [Fact]
    public void ExtremeFloat64Values_SurviveRoundTrip()
    {
        _writer.TryInsert([TypedValue.Of(double.MaxValue)]);
        _writer.TryInsert([TypedValue.Of(double.MinValue)]);
        _writer.TryInsert([TypedValue.Of(double.Epsilon)]);
        _writer.TryInsert([TypedValue.Of(double.NaN)]);

        var row1 = _reader.ReadRow(0, 1);
        row1[0].Value.AsFloat64().Should().Be(double.MaxValue);
        var row2 = _reader.ReadRow(1, 1);
        row2[0].Value.AsFloat64().Should().Be(double.MinValue);
        var row3 = _reader.ReadRow(2, 1);
        row3[0].Value.AsFloat64().Should().Be(double.Epsilon);
        var row4 = _reader.ReadRow(3, 1);
        row4[0].Value.AsFloat64().Should().Be(double.NaN);
    }

    [Fact]
    public void DeleteMiddleRow_RemainingRowsUnaffected()
    {
        _writer.TryInsert([TypedValue.Of(1L)]);
        _writer.TryInsert([TypedValue.Of(2L)]);
        _writer.TryInsert([TypedValue.Of(3L)]);
        _writer.Delete(1);

        var rows = _reader.ReadAllRows(1).ToList();

        rows.Count.Should().Be(2);
        rows[0].row[0].Value.AsInt64().Should().Be(1L);
        rows[1].row[0].Value.AsInt64().Should().Be(3L);
    }

    [Fact]
    public void PageToArray_FromBytes_RowsStillReadable()
    {
        for (int i = 0; i < 10; i++)
            _writer.TryInsert([
                TypedValue.Of(i),
                TypedValue.Of((double)i)
            ]);

        byte[] bytes = _page.ToArray();

        using var restored = RawPage.FromBytes(bytes);
        var restoredReader = new PageReader(restored);

        var rows = restoredReader.ReadAllRows(2).ToList();

        rows.Count.Should().Be(10);
        for (int i = 0; i < 10; i++)
        {
            rows[i].row[0].Value.AsInt64().Should().Be(i);
            rows[i].row[1].Value.AsFloat64().Should().Be(i);
        }
    }

    [Fact]
    public void ReadAllRows_NoGCAllocationPerRow()
    {
        for (int i = 0; i < 100; i++)
            _writer.TryInsert(
            [
                TypedValue.Of(i),
                TypedValue.Of((double)i),
                TypedValue.Of(true)
            ]);

        _ = _reader.ReadAllRows(3).ToList();

        long befor = GC.GetAllocatedBytesForCurrentThread();
        var rows = _reader.ReadAllRows(3).ToList();
        long after = GC.GetAllocatedBytesForCurrentThread();

        long allocatedBytes = after - befor;

        allocatedBytes.Should().BeLessThan(15_000,
            $"Too many allocations: {allocatedBytes} bytes for 100 rows");
    }

}