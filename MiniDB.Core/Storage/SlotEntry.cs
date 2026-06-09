using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MiniDB.Core.Storage;


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SlotEntry
{
    public ushort Offset;
    public ushort Length;
    public ushort Flags;

    public readonly bool IsDeleted =>
        (Flags & PageLayout.SlotFlag_Deleted) != 0;

    public readonly bool IsEmpty =>
        Offset == 0 && Length == 0 && Flags == 0;

    public static SlotEntry Live(ushort offset, ushort length) =>
        new() { Offset = offset, Length = length, Flags = PageLayout.SlotFlag_None };

    public static SlotEntry Deleted() =>
        new() { Offset = 0, Length = 0, Flags = PageLayout.SlotFlag_Deleted };

    static SlotEntry()
    {
        if (Unsafe.SizeOf<SlotEntry>() != PageLayout.SlotSize)
            throw new InvalidOperationException(
                $"SlotEntry must be {PageLayout.SlotSize} bytes, ");
    }
}