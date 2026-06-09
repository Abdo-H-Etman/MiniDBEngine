using System.Runtime.CompilerServices;
using FluentAssertions;
using MiniDB.Core.Storage;

namespace MiniDB.Tests.Storage;

public class SlotEntryTests
{
    [Fact]
    public void SizeOf_SlotEntry_Is6Bytes() =>
        Unsafe.SizeOf<SlotEntry>().Should().Be(6);

    [Fact]
    public void Live_SetsOffsetAndLength()
    {
        var slot = SlotEntry.Live(100, 50);
        slot.Should().Match<SlotEntry>(s =>
            s.Offset == 100 &&
            s.Length == 50 &&
            s.IsDeleted == false);
    }

    [Fact]
    public void Deleted_SetsFlags()
    {
        var slot = SlotEntry.Deleted();
        slot.Should().Match<SlotEntry>(s =>
            s.Offset == 0 &&
            s.Length == 0 &&
            s.IsDeleted == true);
    }

    [Fact]
    public void Live_Slot_IsNotDeleted() =>
        SlotEntry.Live(10, 5).IsDeleted.Should().BeFalse();
}