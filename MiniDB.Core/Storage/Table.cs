using MiniDB.Core.Types;

namespace MiniDB.Core.Storage;

public sealed class Table : IDisposable
{
    private readonly Dictionary<uint, RawPage> _pages = [];
    private readonly PageIdGenerator _idGen = new();

    private uint? _firstPageId;
    private uint? _lastPageId;
    private bool _disposed;

    public string Name { get; }
    public int ColumnCount { get; }

    public Table(string name, int columnCount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Table name cannot be empty.", nameof(name));
        if (columnCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnCount),
                "Table must have at least one column.");
        Name = name;
        ColumnCount = columnCount;
    }

    public (uint PageId, int SlotIndex) Insert(
        ReadOnlySpan<TypedValue> row, string?[]? textValues = null)
    {
        ThrowIfDisposed();

        if (row.Length != ColumnCount)
            throw new ArgumentException(
                $"Row has {row.Length} columns, but table {Name} has {ColumnCount} columns.",
                nameof(row));

        if (_lastPageId is null)
            AllocateAndLinkNewPage();

        var lastPage = _pages[_lastPageId!.Value];
        var writer = new PageWriter(lastPage);
        int slotIndex = writer.TryInsert(row, textValues);

        if (slotIndex >= 0)
            return (_lastPageId.Value, slotIndex);

        AllocateAndLinkNewPage();
        var newPage = _pages[_lastPageId!.Value];
        var newWriter = new PageWriter(newPage);
        int newSlot = newWriter.TryInsert(row, textValues);

        if (newSlot < 0)
            throw new InvalidOperationException(
                "Row is too large to fit even in a freshly allocated page. " +
                "This row exceeds the maximum size a single page can hold.");

        return (_lastPageId.Value, newSlot);
    }

    public IEnumerable<(uint PageId, RawPage Page)> Pages()
    {
        ThrowIfDisposed();
        uint? current = _firstPageId;
        while (current is not null)
        {
            var page = _pages[current.Value];
            yield return (current.Value, page);

            var header = page.ReadHeader();
            current = header.NextPageId == 0 ? null : header.NextPageId;
        }
    }

    public int PageCount => _pages.Count;

    public int RowCount()
    {
        ThrowIfDisposed();
        int total = 0;
        foreach (var page in _pages.Values)
        {
            var reader = new PageReader(page);
            total += reader.LiveRowCount();
        }
        return total;
    }

    public RawPage GetPage(uint pageId)
    {
        ThrowIfDisposed();
        if (!_pages.TryGetValue(pageId, out var page))
            throw new ArgumentException($"No page with ID {pageId} in table {Name}.", nameof(pageId));
        return page;
    }

    private void AllocateAndLinkNewPage()
    {
        uint newId = _idGen.Next();
        var newPage = RawPage.Allocate(newId);
        _pages[newId] = newPage;

        if (_lastPageId is null)
        {
            _firstPageId = newId;
            _lastPageId = newId;
            return;
        }

        var prevPage = _pages[_lastPageId.Value];
        var prevHeader = prevPage.ReadHeader();
        prevHeader.NextPageId = newId;
        prevPage.WriteHeader(prevHeader);

        _lastPageId = newId;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, nameof(Table));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var page in _pages.Values)
            page.Dispose();

        _pages.Clear();
    }
}