using System.Text;
using FluentAssertions;
using MiniDB.Core.Storage;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Storage;

public class PageWriterTests : IDisposable
{
    private readonly RawPage _page = RawPage.Allocate(pageId: 1);
    private readonly PageWriter _writer;

    public PageWriterTests() => _writer = new PageWriter(_page);

    public void Dispose() => _page.Dispose();

    // ── basic insert ──────────────────────────────────────────────────────────

    [Fact]
    public void Insert_SingleRow_ReturnsSlotZero()
    {
        TypedValue[] row = [TypedValue.Of(1L), TypedValue.Of(2L)];

        int slot = _writer.TryInsert(row);

        slot.Should().Be(0);
    }

    [Fact]
    public void Insert_TwoRows_ReturnConsecutiveSlots()
    {
        TypedValue[] row = [TypedValue.Of(1L)];

        int s0 = _writer.TryInsert(row);
        int s1 = _writer.TryInsert(row);

        s0.Should().Be(0);
        s1.Should().Be(1);
    }

    [Fact]
    public void Insert_UpdatesSlotCount()
    {
        TypedValue[] row = [TypedValue.Of(1L)];

        _writer.TryInsert(row);
        _writer.TryInsert(row);

        _page.ReadHeader().SlotCount.Should().Be(2);
    }

    [Fact]
    public void Insert_MovesFreeSpaceOffsetLeft()
    {
        int before = _page.ReadHeader().FreeSpaceOffset;

        TypedValue[] row = [TypedValue.Of(1L), TypedValue.Of(2L)];

        _writer.TryInsert(row);

        int after = _page.ReadHeader().FreeSpaceOffset;

        after.Should().Be(before - 18);
    }

    [Fact]
    public void Insert_SlotPointsToCorrectOffset()
    {
        TypedValue[] row = [TypedValue.Of(99L)];

        _writer.TryInsert(row);

        var header = _page.ReadHeader();
        var slot = _page.ReadSlot(0);

        slot.Offset.Should().Be(header.FreeSpaceOffset);
        slot.Length.Should().Be(9);
    }

    // ── multiple rows ─────────────────────────────────────────────────────────

    [Fact]
    public void Insert_50Rows_AllGetDistinctSlots()
    {
        var slots = new HashSet<int>();

        for (int i = 0; i < 50; i++)
        {
            int slot = _writer.TryInsert(
            [
                TypedValue.Of((long)i),
                TypedValue.Of((double)i),
                TypedValue.Of(i % 2 == 0)
            ]);

            slot.Should()
                .BeGreaterThanOrEqualTo(0, $"Insert {i} failed unexpectedly");

            slots.Add(slot);
        }

        slots.Should().HaveCount(50);
    }

    [Fact]
    public void Insert_50Rows_SlotDataDoesNotOverlap()
    {
        var slotRanges = new List<(int start, int end)>();

        for (int i = 0; i < 50; i++)
        {
            _writer.TryInsert(
            [
                TypedValue.Of((long)i),
                TypedValue.Of((long)i * 2)
            ]);

            var slot = _page.ReadSlot(i);
            slotRanges.Add((slot.Offset, slot.Offset + slot.Length));
        }

        for (int i = 0; i < slotRanges.Count; i++)
        {
            for (int j = i + 1; j < slotRanges.Count; j++)
            {
                var a = slotRanges[i];
                var b = slotRanges[j];

                bool overlaps = a.start < b.end && b.start < a.end;

                overlaps.Should()
                    .BeFalse($"Slots {i} and {j} data areas overlap");
            }
        }
    }

    // ── page full ─────────────────────────────────────────────────────────────

    [Fact]
    public void Insert_WhenPageFull_ReturnsMinus1()
    {
        TypedValue[] row = [TypedValue.Of(0L)];

        int inserted = 0;

        while (_writer.TryInsert(row) >= 0)
            inserted++;

        int overflow = _writer.TryInsert(row);

        overflow.Should().Be(-1);
        inserted.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Insert_WhenPageFull_HeaderUnchanged()
    {
        TypedValue[] row = [TypedValue.Of(0L)];

        while (_writer.TryInsert(row) >= 0)
        {
        }

        var headerBefore = _page.ReadHeader();

        _writer.TryInsert(row);

        var headerAfter = _page.ReadHeader();

        headerAfter.SlotCount.Should().Be(headerBefore.SlotCount);
        headerAfter.FreeSpaceOffset.Should().Be(headerBefore.FreeSpaceOffset);
    }

    // ── delete ────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_MarksSlotAsDeleted()
    {
        TypedValue[] row = [TypedValue.Of(1L)];

        int slot = _writer.TryInsert(row);

        _writer.Delete(slot);

        _page.ReadSlot(slot).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Delete_InvalidSlot_Throws()
    {
        var act = () => _writer.Delete(0);

        act.Should()
           .Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LiveRowCount_ExcludesDeleted()
    {
        TypedValue[] row = [TypedValue.Of(1L)];

        _writer.TryInsert(row);
        _writer.TryInsert(row);
        _writer.TryInsert(row);

        _writer.Delete(1);

        _writer.LiveRowCount().Should().Be(2);
    }

    // ── null and mixed types ──────────────────────────────────────────────────

    [Fact]
    public void Insert_NullColumns_Succeed()
    {
        TypedValue[] row =
        [
            TypedValue.Null,
            TypedValue.Of(5L),
            TypedValue.Null
        ];

        int slot = _writer.TryInsert(row);

        slot.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Insert_EmptyRow_Throws()
    {
        var act = () => _writer.TryInsert(ReadOnlySpan<TypedValue>.Empty);

        act.Should()
           .Throw<ArgumentException>();
    }

    // ── text columns ──────────────────────────────────────────────────────────

    [Fact]
    public void Insert_TextColumn_SlotLengthIncludesTextBytes()
    {
        var row = new TypedValue[]
        {
            new(DbType.Text, DbValue.Null)
        };

        var texts = new string?[]
        {
            "hello"
        };

        int slotIdx = _writer.TryInsert(row, texts);

        slotIdx.Should().BeGreaterThanOrEqualTo(0);

        var slot = _page.ReadSlot(slotIdx);

        slot.Length.Should().Be(14);
    }

    // ── page invariants after many inserts ───────────────────────────────────

    [Fact]
    public void AfterInserts_SlotArrayAndDataNeverOverlap()
    {
        for (int i = 0; i < 100; i++)
        {
            _writer.TryInsert(
            [
                TypedValue.Of((long)i),
                TypedValue.Of((long)i)
            ]);
        }

        var header = _page.ReadHeader();

        int slotArrayEnd =
            PageLayout.SlotArrayOffset +
            header.SlotCount * PageLayout.SlotSize;

        ((int)header.FreeSpaceOffset).Should()
            .BeGreaterThanOrEqualTo(
                slotArrayEnd,
                $"Data area ({header.FreeSpaceOffset}) overlaps slot array end ({slotArrayEnd})");
    }
}