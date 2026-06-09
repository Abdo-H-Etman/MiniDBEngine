namespace MiniDB.Core.Storage;

public static class PageLayout
{
    public const int PageSize = 4096;
    public const int HeaderSize = 16;
    public const int SlotSize = 6;
    public const int SlotArrayOffset = HeaderSize;
    public const int MaxSlots = (PageSize - HeaderSize) / SlotSize;

    public const int OffPageId = 0;
    public const int OffFreeSpaceOffset = 4;
    public const int OffNumSlots = 6;
    public const int OffFlags = 8;
    public const int OffNextPageId = 9;
    public const int OffPrevPageId = 13;

    public const ushort SlotFlag_None = 0x0000;
    public const ushort SlotFlag_Deleted = 0x0001;
}