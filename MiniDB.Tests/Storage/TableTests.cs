using FluentAssertions;
using MiniDB.Core.Storage;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Storage;

public class TableTests : IDisposable
{
    private readonly Table _table = new("users", columnCount: 2);

    public void Dispose() => _table.Dispose();


    [Fact]
    public void Construction_SetsNameAndColumnCount()
    {
        _table.Name.Should().Be("users");
        _table.ColumnCount.Should().Be(2);
    }

    [Fact]
    public void Construction_EmptyName_Throws()
    {
        Action act = () => new Table("", 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construction_ZeroColumns_Throws()
    {
        Action act = () => new Table("t", 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewTable_HasZeroPages()
    {
        _table.PageCount.Should().Be(0);
    }

    [Fact]
    public void NewTable_HasZeroRows()
    {
        _table.RowCount().Should().Be(0);
    }


    [Fact]
    public void Insert_FirstRow_AllocatesFirstPage()
    {
        _table.Insert([TypedValue.Of(1L), TypedValue.Of(2L)]);
        _table.PageCount.Should().Be(1);
    }

    [Fact]
    public void Insert_FirstRow_ReturnsPageOneSlotZero()
    {
        var (pageId, slot) = _table.Insert([TypedValue.Of(1L), TypedValue.Of(2L)]);
        pageId.Should().Be(1u);
        slot.Should().Be(0);
    }

    [Fact]
    public void Insert_WrongColumnCount_Throws()
    {
        Action act = () => _table.Insert([TypedValue.Of(1L)]);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*2 columns*");
    }

    [Fact]
    public void Insert_IncreasesRowCount()
    {
        _table.Insert([TypedValue.Of(1L), TypedValue.Of(2L)]);
        _table.Insert([TypedValue.Of(3L), TypedValue.Of(4L)]);
        _table.RowCount().Should().Be(2);
    }

    [Fact]
    public void Insert_SameRowMultipleTimes_SlotsIncrement()
    {
        var (_, slot0) = _table.Insert([TypedValue.Of(1L), TypedValue.Of(1L)]);
        var (_, slot1) = _table.Insert([TypedValue.Of(2L), TypedValue.Of(2L)]);
        var (_, slot2) = _table.Insert([TypedValue.Of(3L), TypedValue.Of(3L)]);

        slot0.Should().Be(0);
        slot1.Should().Be(1);
        slot2.Should().Be(2);
    }


    [Fact]
    public void Insert_ManyRows_SpansMultiplePages()
    {
        for (int i = 0; i < 500; i++)
            _table.Insert([TypedValue.Of(i), TypedValue.Of(i)]);

        _table.PageCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Insert_SpanningPages_AllRowsReadableInOrder()
    {
        var inserted = new List<long>();
        for (int i = 0; i < 500; i++)
        {
            _table.Insert([TypedValue.Of(i), TypedValue.Of(i)]);
            inserted.Add(i);
        }

        var allValues = new List<long>();
        foreach (var (_, page) in _table.Pages())
        {
            var reader = new PageReader(page);
            foreach (var (_, row) in reader.ReadAllRows(2))
                allValues.Add(row[0].Value.AsInt64());
        }

        allValues.Should().Equal(inserted);
    }

    [Fact]
    public void Pages_ChainOrderMatchesAllocationOrder()
    {
        for (int i = 0; i < 500; i++)
            _table.Insert([TypedValue.Of(i), TypedValue.Of(i)]);

        var pageIds = _table.Pages().Select(p => p.PageId).ToList();

        pageIds.Should().BeInAscendingOrder();
        pageIds.Should().Equal(Enumerable.Range(1, pageIds.Count).Select(i => (uint)i));
    }

    [Fact]
    public void Pages_LastPage_HasNextPageIdZero()
    {
        for (int i = 0; i < 500; i++)
            _table.Insert([TypedValue.Of(i), TypedValue.Of(i)]);

        var lastPage = _table.Pages().Last().Page;
        var lastHeader = lastPage.ReadHeader();
        lastHeader.NextPageId.Should().Be(0u);
    }

    [Fact]
    public void Pages_NonLastPages_HaveNonZeroNextPageId()
    {
        for (int i = 0; i < 500; i++)
            _table.Insert([TypedValue.Of(i), TypedValue.Of(i)]);

        var pages = _table.Pages().ToList();
        for (int i = 0; i < pages.Count - 1; i++)
        {
            var header = pages[i].Page.ReadHeader();
            header.NextPageId.Should().NotBe(0u);
            header.NextPageId.Should().Be(pages[i + 1].PageId);
        }
    }


    [Fact]
    public void GetPage_ValidId_ReturnsPage()
    {
        _table.Insert([TypedValue.Of(1L), TypedValue.Of(2L)]);
        var page = _table.GetPage(1u);
        page.ReadHeader().PageId.Should().Be(1u);
    }

    [Fact]
    public void GetPage_InvalidId_Throws()
    {
        Action act = () => _table.GetPage(999u);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*999*");
    }


    [Fact]
    public void Insert_TextColumns_SurviveAcrossManyRows()
    {
        using var textTable = new Table("docs", columnCount: 1);

        var names = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            string text = $"document-{i}";
            names.Add(text);
            var row = new TypedValue[] { new(DbType.Text, DbValue.Null) };
            textTable.Insert(row, [text]);
        }

        var readBack = new List<string>();
        foreach (var (_, page) in textTable.Pages())
        {
            var reader = new PageReader(page);
            foreach (var (_, row) in reader.ReadAllRows(1))
                readBack.Add(row[0].ResolvedText!);
        }

        readBack.Should().Equal(names);
    }


    [Fact]
    public void Dispose_DisposesAllPages()
    {
        var table = new Table("temp", 1);
        table.Insert([TypedValue.Of(1L)]);
        table.Insert([TypedValue.Of(2L)]);

        table.Dispose();

        Action act = () => table.Insert([TypedValue.Of(3L)]);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var table = new Table("temp", 1);
        table.Dispose();
        Action act = () => table.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void AfterDispose_Pages_Throws()
    {
        var table = new Table("temp", 1);
        table.Insert([TypedValue.Of(1L)]);
        table.Dispose();

        Action act = () => table.Pages().ToList();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AfterDispose_RowCount_Throws()
    {
        var table = new Table("temp", 1);
        table.Dispose();

        Action act = () => table.RowCount();
        act.Should().Throw<ObjectDisposedException>();
    }
}