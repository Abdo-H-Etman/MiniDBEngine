using System.Text;
using MiniDB.Core.Types;

namespace MiniDB.Core.Storage;

public sealed class PageWriter
{
    private readonly RawPage _page;

    public PageWriter(RawPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    public int TryInsert(ReadOnlySpan<TypedValue> row, string?[]? textValues = null)
    {
        if (row.IsEmpty)
            throw new ArgumentException("Cannot insert empty row.", nameof(row));

        if (textValues != null && textValues.Length != row.Length)
            throw new ArgumentException(
                $"textValues length ({textValues.Length}) must match row length ({row.Length}).",
                nameof(textValues));

        byte[]?[]? textBytes = null;
        int totalTextBytes = 0;

        if (textValues != null)
        {
            textBytes = new byte[]?[row.Length];
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i].Type == DbType.Text
                    && !row[i].IsNull
                    && textValues[i] is { } s)
                {
                    byte[] encoded = Encoding.UTF8.GetBytes(s);
                    textBytes[i] = encoded;
                    totalTextBytes += encoded.Length;
                }
            }
        }

        int rowSize = RowSerializer.CalculateSize(row.Length, totalTextBytes);

        var header = _page.ReadHeader();
        if (!header.CanFit(rowSize))
            return -1;

        int rawOffset = header.FreeSpaceOffset - rowSize;
        if (rawOffset < PageLayout.SlotArrayOffset)
            throw new InvalidOperationException(
                $"Computed FreeSpaceOffset {rawOffset} would overlap the slot array. " +
                "CanFit check should have prevented this.");

        ushort newFreeSpaceOffset = (ushort)rawOffset;

        var dataSpan = _page.GetDataSpan(newFreeSpaceOffset, rowSize);
        int written = RowSerializer.Serialize(row, textBytes, dataSpan);

        if (written != rowSize)
            throw new InvalidOperationException(
                $"Serialized {written} bytes but expected {rowSize}. " +
                "This is a bug in RowSerializer.");

        int newSlotIndex = header.SlotCount;
        _page.WriteSlot(newSlotIndex,
                SlotEntry.Live(newFreeSpaceOffset, (ushort)rowSize));

        header.FreeSpaceOffset = newFreeSpaceOffset;
        header.SlotCount = (ushort)(header.SlotCount + 1);
        _page.WriteHeader(header);

        return newSlotIndex;
    }

    public void Delete(int slotIndex)
    {
        var header = _page.ReadHeader();
        if (slotIndex < 0 || slotIndex >= header.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot {slotIndex} does not exist (SlotCount={header.SlotCount}).");

        _page.WriteSlot(slotIndex, SlotEntry.Deleted());
    }

    public int LiveRowCount()
    {
        var header = _page.ReadHeader();
        int count = 0;
        for (int i = 0; i < header.SlotCount; i++)
            if (!_page.ReadSlot(i).IsDeleted)
                count++;
        return count;
    }
}