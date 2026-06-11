using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MiniDB.Core.Storage;

public sealed unsafe class RawPage : IDisposable
{
    private byte* _ptr;
    private bool _disposed;

    public static RawPage Allocate(uint pageId)
    {
        var page = new RawPage();
        page._ptr = (byte*)NativeMemory.AlignedAlloc(
            byteCount: PageLayout.PageSize,
            alignment: 8
        );

        NativeMemory.Clear(page._ptr, PageLayout.PageSize);

        page.WriteHeader(PageHeader.Create(pageId));
        return page;
    }

    public static RawPage FromBytes(ReadOnlySpan<byte> source)
    {
        if (source.Length != PageLayout.PageSize)
            throw new ArgumentException(
                $"Source must be exactly {PageLayout.PageSize} bytes, " +
                $"got {source.Length} bytes.");

        var page = new RawPage();
        page._ptr = (byte*)NativeMemory.AlignedAlloc(
            byteCount: PageLayout.PageSize,
            alignment: 8
        );

        source.CopyTo(new Span<byte>(page._ptr, PageLayout.PageSize));
        return page;
    }

    private RawPage() { }

    public PageHeader ReadHeader()
    {
        ThrowIfDisposed();
        return Unsafe.ReadUnaligned<PageHeader>(_ptr);
    }

    public void WriteHeader(in PageHeader header)
    {
        ThrowIfDisposed();
        Unsafe.WriteUnaligned(_ptr, header);
    }

    public SlotEntry ReadSlot(int slotIndex)
    {
        ThrowIfDisposed();
        ValidateSlotIndexForRead(slotIndex);

        byte* slotPtr = _ptr + PageLayout.SlotArrayOffset + slotIndex * PageLayout.SlotSize;
        return Unsafe.ReadUnaligned<SlotEntry>(slotPtr);
    }

    public void WriteSlot(int slotIndex, in SlotEntry slot)
    {
        ThrowIfDisposed();
        ValidateSlotIndexForWrite(slotIndex);

        byte* slotPtr = _ptr + PageLayout.SlotArrayOffset +
            slotIndex * PageLayout.SlotSize;
        Unsafe.WriteUnaligned(slotPtr, slot);
    }

    public ReadOnlySpan<byte> ReadSlotData(in SlotEntry slot)
    {
        ThrowIfDisposed();
        if (slot.IsEmpty || slot.IsDeleted)
            throw new InvalidOperationException("Cannot read data from an empty or deleted slot.");
        return new ReadOnlySpan<byte>(_ptr + slot.Offset, slot.Length);
    }

    public Span<byte> GetDataSpan(int offset, int length)
    {
        ThrowIfDisposed();
        if (offset < PageLayout.HeaderSize || offset + length > PageLayout.PageSize)
            throw new ArgumentOutOfRangeException(
                nameof(offset), $"Data span [{offset}..{offset + length}] is outside the page");

        return new Span<byte>(_ptr + offset, length);
    }

    public byte[] ToArray()
    {
        ThrowIfDisposed();
        var result = new byte[PageLayout.PageSize];
        new ReadOnlySpan<byte>(_ptr, PageLayout.PageSize).CopyTo(result);
        return result;
    }


    private void ValidateSlotIndexForRead(int index)
    {
        var header = ReadHeader();
        if (index < 0 || index >= header.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} out of range (SlotCount={header.SlotCount}).");
    }

    private void ValidateSlotIndexForWrite(int index)
    {
        var header = ReadHeader();
        if (index < 0 || index > header.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} out of range (SlotCount={header.SlotCount}).");
    }
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RawPage));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMemory.AlignedFree(_ptr);
        _ptr = null;
    }
}