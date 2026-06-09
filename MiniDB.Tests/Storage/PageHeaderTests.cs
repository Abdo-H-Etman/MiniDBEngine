using System.Runtime.CompilerServices;
using FluentAssertions;
using MiniDB.Core.Storage;

namespace MiniDB.Tests.Storage;

public class PageHeaderTests
{
    [Fact]
    public void PageHeader_IsExactly16Bytes() =>
        Unsafe.SizeOf<PageHeader>().Should().Be(16);

    [Fact]
    public void Create_SetsPageId() =>
        PageHeader.Create(42).PageId.Should().Be(42u);

    [Fact]
    public void Create_FreeSpaceOffset_IsPageSize() =>
        PageHeader.Create(1).FreeSpaceOffset.Should().Be(PageLayout.PageSize);

    [Fact]
    public void Create_SlotCount_IsZero() =>
        PageHeader.Create(1).SlotCount.Should().Be(0);

    [Fact]
    public void Create_NextPageId_IsZero() =>
        PageHeader.Create(1).NextPageId.Should().Be(0u);

    [Fact]
    public void FreeBytes_EmptyPage_IsPageSizeMinusHeaderMinusSlotArray()
    {
        var h = PageHeader.Create(1);

        // SlotArrayEnd = HeaderSize + 0 slots = 16
        // FreeBytes = PageSize - 16 = 4080
        h.FreeBytes.Should().Be(
            PageLayout.PageSize - PageLayout.HeaderSize);
    }

    [Fact]
    public void CanFit_SmallRow_ReturnsTrue()
    {
        var h = PageHeader.Create(1);

        h.CanFit(100).Should().BeTrue();
    }

    [Fact]
    public void CanFit_EntireFreeSpace_ReturnsFalse()
    {
        // A row exactly equal to FreeBytes won't fit
        // because the slot entry itself also needs SlotSize bytes
        var h = PageHeader.Create(1);

        h.CanFit(h.FreeBytes).Should().BeFalse();
    }

    [Fact]
    public void CanFit_RowPlusSlot_ExactlyFits()
    {
        var h = PageHeader.Create(1);
        int maxRowSize = h.FreeBytes - PageLayout.SlotSize;

        h.CanFit(maxRowSize).Should().BeTrue();
    }
}