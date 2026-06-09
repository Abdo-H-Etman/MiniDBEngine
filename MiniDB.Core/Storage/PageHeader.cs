using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MiniDB.Core.Storage;


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PageHeader
{
    public uint PageId;
    public ushort FreeSpaceOffset;
    public ushort SlotCount;
    public byte Flags;
    public uint NextPageId;

    private byte _padd0;
    private byte _padd1;
    private byte _padd2;

    public readonly int SlotArrayEnd =>
        PageLayout.SlotArrayOffset + SlotCount * PageLayout.SlotSize;

    public readonly int FreeBytes =>
        FreeSpaceOffset - SlotArrayEnd;

    public readonly bool CanFit(int rowByteSize) =>
        FreeBytes >= rowByteSize + PageLayout.SlotSize;

    public static PageHeader Create(uint pageId) =>
        new()
        {
            PageId = pageId,
            FreeSpaceOffset = PageLayout.PageSize,
            SlotCount = 0,
            Flags = 0,
            NextPageId = 0
        };

    static PageHeader()
    {
        if (Unsafe.SizeOf<PageHeader>() != PageLayout.HeaderSize)
            throw new InvalidOperationException(
                $"PageHeader size is {Unsafe.SizeOf<PageHeader>()} bytes, " +
                $"expected {PageLayout.HeaderSize}. Check struct layout.");
    }
}