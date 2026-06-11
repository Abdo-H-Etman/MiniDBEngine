using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MiniDB.Core.Types;

namespace MiniDB.Core.Storage;

public static class RowSerializer
{
    public const int BytesPerColumn = 9;

    public static int CalculateSize(ReadOnlySpan<TypedValue> raw)
    {
        int size = raw.Length * BytesPerColumn;
        foreach (ref readonly var col in raw)
        {
            if (col.Type == DbType.Text && !col.IsNull)
            {
                size += (int)col.Value.AsTextRef().length;
            }
        }
        return size;
    }

    public static int CalculateSize(int columnCount, int totalTextBytes) =>
        columnCount * BytesPerColumn + totalTextBytes;

    public static int Serialize(
        ReadOnlySpan<TypedValue> row,
        byte[]?[]? textBytes,
        Span<byte> dest
    )
    {
        int columnAreaSize = row.Length * BytesPerColumn;
        int textAreaOffset = columnAreaSize;
        int textWritePos = textAreaOffset;

        for (int i = 0; i < row.Length; i++)
        {
            ref readonly var col = ref row[i];
            var colOffset = i * BytesPerColumn;

            dest[colOffset] = (byte)col.Type;

            if (col.Type == DbType.Text
                && !col.IsNull
                && textBytes?[i] is { } tb)
            {
                tb.CopyTo(dest[textWritePos..]);

                var textRef = DbValue.FromTextRef(
                    offset: (uint)textWritePos,
                    length: (uint)tb.Length);

                WriteDbValue(dest, colOffset + 1, textRef);
                textWritePos += tb.Length;
            }
            else
            {
                WriteDbValue(dest, colOffset + 1, col.Value);
            }
        }

        return textWritePos;
    }

    public static TypedValue[] Deserialize(
        ReadOnlySpan<byte> src,
        int columnCount,
        int pageBase)
    {
        var row = new TypedValue[columnCount];

        for (int i = 0; i < columnCount; i++)
        {
            int colOffset = i * BytesPerColumn;
            var type = (DbType)src[colOffset];
            var value = ReadDbValue(src, colOffset + 1);

            if (type == DbType.Text && value.RawBits() != 0)
            {
                var (rowRelOffset, length) = value.AsTextRef();
                uint pageRelOffset = (uint)(pageBase + rowRelOffset);
                value = DbValue.FromTextRef(pageRelOffset, length);
            }

            row[i] = new TypedValue(type, value);
        }

        return row;
    }

    private static void WriteDbValue(Span<byte> dest, int offset, DbValue value)
    {
        ulong raw = value.RawBits();
        Unsafe.WriteUnaligned(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(dest), offset),
            raw);
    }

    private static DbValue ReadDbValue(ReadOnlySpan<byte> src, int offset)
    {
        ulong raw = Unsafe.ReadUnaligned<ulong>(
            ref Unsafe.Add(
                ref MemoryMarshal.GetReference(src),
                offset));

        return DbValue.FromRawBits(raw);
    }
}