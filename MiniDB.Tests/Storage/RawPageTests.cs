using FluentAssertions;
using MiniDB.Core.Storage;

namespace MiniDB.Tests.Storage;

public class RawPageTests : IDisposable
{
    private readonly RawPage _page = RawPage.Allocate(1);
    public void Dispose() => _page.Dispose();

    // ── Allocation ───────────────────────────────────────────────────────────────
    [Fact]
    public void Allocate_CreatesPageWithCorrectId()
    {
        var h = _page.ReadHeader();
        h.PageId.Should().Be(1u);
    }

    [Fact]
    public void Allocate_CreatesPageWithCorrectFreeSpaceOffset()
    {
        var h = _page.ReadHeader();
        h.FreeSpaceOffset.Should().Be(PageLayout.PageSize);
    }

    [Fact]
    public void Allocate_CreatesPageWithEmptySlotArray()
    {
        var h = _page.ReadHeader();
        h.SlotCount.Should().Be(0);
    }

    // ── Header round-trip ─────────────────────────────────────────────────────────
    [Fact]
    public void WriteHeader_ThenReadHeader_ReturnsSameHeader()
    {
        var original = PageHeader.Create(42);
        _page.WriteHeader(original);

        var readHeader = _page.ReadHeader();
        readHeader.PageId.Should().Be(42u);
        readHeader.FreeSpaceOffset.Should().Be(original.FreeSpaceOffset);
        readHeader.SlotCount.Should().Be(original.SlotCount);
    }

    [Fact]
    public void WriteHeader_UpdateSlotCount_Presists()
    {
        var h = PageHeader.Create(1);
        h.SlotCount = 5;
        h.FreeSpaceOffset = 4000;
        _page.WriteHeader(h);

        var readHeader = _page.ReadHeader();
        readHeader.SlotCount.Should().Be(5);
        readHeader.FreeSpaceOffset.Should().Be(4000);
    }

    // ── Slot entry access ─────────────────────────────────────────────────────────
    [Fact]
    public void WriteSlot_ThenReadSlot_RoundTrips()
    {
        var h = _page.ReadHeader();
        h.SlotCount = 1;
        _page.WriteHeader(h);

        var slot = SlotEntry.Live(100, 50);
        _page.WriteSlot(0, slot);
        var readSlot = _page.ReadSlot(0);

        readSlot.Offset.Should().Be(100);
        readSlot.Length.Should().Be(50);
        readSlot.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void WriteSlot_Deleted_RoundTrips()
    {
        var h = _page.ReadHeader();
        h.SlotCount = 1;
        _page.WriteHeader(h);

        _page.WriteSlot(0, SlotEntry.Deleted());

        _page.ReadSlot(0).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void WriteSlot_InvalidIndex_Throws()
    {
        FluentActions.Invoking(() => _page.WriteSlot(-1, SlotEntry.Live(0, 0)))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Slot index -1 out of range (SlotCount=0).*");
    }

    [Fact]
    public void ReadSlot_InvalidIndex_Throws()
    {
        FluentActions.Invoking(() => _page.ReadSlot(0))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Slot index 0 out of range (SlotCount=0).*");
    }

    // ── Slot data access ─────────────────────────────────────────────────────────
    [Fact]
    public void GetDataSpan_WriteData_ThenRead_ReturnsSameData()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        int offset = PageLayout.PageSize - data.Length;
        data.CopyTo(_page.GetDataSpan(offset, data.Length));

        var h = _page.ReadHeader();
        h.SlotCount = 1;
        _page.WriteHeader(h);

        _page.WriteSlot(0,
            SlotEntry.Live((ushort)offset, (ushort)data.Length));


        var slot = _page.ReadSlot(0);
        var readData = _page.ReadSlotData(slot);
        readData.SequenceEqual(data).Should().BeTrue();
    }

    [Fact]
    public void GetDataSpan_InvalidOffset_Throws()
    {
        FluentActions.Invoking(() => _page.GetDataSpan(PageLayout.HeaderSize - 10, 5))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"*Data span [{PageLayout.HeaderSize - 10}..{PageLayout.HeaderSize - 5}] is outside the page*");
    }

    // ── ToArray / FromBytes round-trip ───────────────────────────────────────────────────────────────
    [Fact]
    public void ToArray_FromBytes_RoundTrips()
    {
        var h = _page.ReadHeader();
        h.SlotCount = 3;
        h.FreeSpaceOffset = 2048;
        h.NextPageId = 7u;
        _page.WriteHeader(h);

        byte[] bytes = _page.ToArray();
        bytes.Length.Should().Be(PageLayout.PageSize);

        using var restored = RawPage.FromBytes(bytes);
        var restoredHeader = restored.ReadHeader();

        restoredHeader.SlotCount.Should().Be(3);
        restoredHeader.FreeSpaceOffset.Should().Be(2048);
        restoredHeader.NextPageId.Should().Be(7u);
    }

    [Fact]
    public void FromBytes_InvalidLength_Throws()
    {
        FluentActions.Invoking(() => RawPage.FromBytes(new byte[100]))
            .Should().Throw<ArgumentException>()
            .WithMessage(
                $"Source must be exactly {PageLayout.PageSize} bytes, " +
                "got 100 bytes."
            );
    }

    // ── Dispose ───────────────────────────────────────────────────────────────
    [Fact]
    public void AfterDispose_AccessingPage_Throws()
    {
        _page.Dispose();
        FluentActions.Invoking(() => _page.ReadHeader())
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _page.Dispose();
        FluentActions.Invoking(() => _page.Dispose())
            .Should().NotThrow();
    }
}