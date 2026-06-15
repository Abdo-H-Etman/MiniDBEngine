using System.Security.Principal;
using System.Text;
using MiniDB.Core.Types;

namespace MiniDB.Core.Storage;

public sealed class PageReader
{
    private readonly RawPage _page;

    public PageReader(RawPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }

    public TypedValue[] ReadRow(int slotIndex, int columnCount)
    {
        ValidateColumnCount(columnCount);

        var header = _page.ReadHeader();
        ValidateSlotIndex(slotIndex, header.SlotCount);

        var slot = _page.ReadSlot(slotIndex);
        if (slot.IsDeleted)
            throw new InvalidOperationException(
                $"slot {slotIndex} is deleted and cannot be read.");

        return DeserializeSlot(slot, columnCount);
    }

    public TypedValue[]? TryReadRow(int slotIndex, int columnCount)
    {
        ValidateColumnCount(columnCount);

        var header = _page.ReadHeader();
        if (slotIndex < 0 || slotIndex > header.SlotCount)
            return null;

        var slot = _page.ReadSlot(slotIndex);
        if (slot.IsDeleted)
            return null;

        return DeserializeSlot(slot, columnCount);
    }

    public IEnumerable<(int slotIndex, TypedValue[] row)> ReadAllRows(int columnCount)
    {
        ValidateColumnCount(columnCount);

        var header = _page.ReadHeader();
        for (int i = 0; i < header.SlotCount; i++)
        {
            var slot = _page.ReadSlot(i);
            if (slot.IsDeleted) continue;
            yield return (i, DeserializeSlot(slot, columnCount));
        }
    }

    public IEnumerable<(int slotIndex, TypedValue[] row)> ReadWhere(
        int columnCount,
        Func<TypedValue[], bool> predicate
    )
    {
        foreach (var (idx, row) in ReadAllRows(columnCount))
            if (predicate(row))
                yield return (idx, row);
    }

    public int SlotCount => _page.ReadHeader().SlotCount;

    public int LiveRowCount()
    {
        var header = _page.ReadHeader();
        int count = 0;
        for (int i = 0; i < header.SlotCount; i++)
            if (!_page.ReadSlot(i).IsDeleted)
                count++;

        return count;
    }

    public int FreeBytes => _page.ReadHeader().FreeBytes;

    private TypedValue[] DeserializeSlot(in SlotEntry slot, int columnCount)
    {
        var rawBytes = _page.ReadSlotData(slot);

        var row = RowSerializer.Deserialize(rawBytes, columnCount, slot.Offset);

        ResolveTextColumns(row);

        return row;
    }

    private void ResolveTextColumns(TypedValue[] row)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i].Type != DbType.Text) continue;
            if (row[i].IsNull) continue;

            var (pageOffset, length) = row[i].Value.AsTextRef();

            if (length == 0)
            {
                row[i] = TypedValue.FromString(string.Empty);
                continue;
            }

            var textSpan = _page.GetReadOnlyDataSpan((int)pageOffset, (int)length);
            string text = Encoding.UTF8.GetString(textSpan);
            row[i] = TypedValue.FromString(text);
        }
    }

    private static void ValidateColumnCount(int columnCount)
    {
        if (columnCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnCount),
                "columnCount must be at least 1.");
    }

    private static void ValidateSlotIndex(int slotIndex, int slotCount)
    {
        if (slotIndex < 0 || slotIndex >= slotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex),
                $"Slot index {slotIndex} is out of range (SlotCount={slotCount}).");
    }
}