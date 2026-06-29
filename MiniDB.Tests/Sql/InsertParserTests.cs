using FluentAssertions;
using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Sql.Parser;

namespace MiniDB.Tests.Sql;

public class InsertParserTests
{
    private static InsertStatement ParseInsert(string sql) =>
        new Parser(sql).ParseInsert();


    [Fact]
    public void Insert_WithColumnList()
    {
        var stmt = ParseInsert("INSERT INTO users(id, name) VALUES (1, 'Ali')");

        stmt.Table.Should().Be("users");
        stmt.HasColumnList.Should().BeTrue();
        stmt.Columns.Should().Equal("id", "name");
        stmt.RowCount.Should().Be(1);

        var row = stmt.Rows[0];
        row[0].Value.Should().Be(1L);
        row[1].Value.Should().Be("Ali");
    }

    [Fact]
    public void Insert_WithoutColumnList()
    {
        var stmt = ParseInsert("INSERT INTO users VALUES (1, 'Ali', TRUE)");

        stmt.HasColumnList.Should().BeFalse();
        stmt.Rows[0].Should().HaveCount(3);
    }

    [Fact]
    public void Insert_MultiRow()
    {
        var stmt = ParseInsert(
            "INSERT INTO users (id) VALUES (1), (2), (3)");

        stmt.RowCount.Should().Be(3);
        stmt.Rows[0][0].Value.Should().Be(1L);
        stmt.Rows[1][0].Value.Should().Be(2L);
        stmt.Rows[2][0].Value.Should().Be(3L);
    }

    [Fact]
    public void Insert_AllLiteralTypes()
    {
        var stmt = ParseInsert(
            "INSERT INTO t VALUES (42, 3.14, 'hello', TRUE, FALSE, NULL)");

        var row = stmt.Rows[0];
        row[0].Value.Should().Be(42L);
        row[1].Value.Should().Be(3.14);
        row[2].Value.Should().Be("hello");
        row[3].Value.Should().Be(true);
        row[4].Value.Should().Be(false);
        row[5].Value.Should().BeNull();
    }

    [Fact]
    public void Insert_NegativeNumberLiteral()
    {
        var stmt = ParseInsert("INSERT INTO t VALUES (-50)");
        stmt.Rows[0][0].Value.Should().Be(-50L);
    }


    [Fact]
    public void ParseStatement_DispatchesToInsert()
    {
        var stmt = new Parser("INSERT INTO t VALUES (1)").ParseStatement();
        stmt.Should().BeOfType<InsertStatement>();
    }

    [Fact]
    public void ParseStatement_DispatchesToSelect()
    {
        var stmt = new Parser("SELECT * FROM t").ParseStatement();
        stmt.Should().BeOfType<SelectStatement>();
    }


    [Fact]
    public void Insert_MissingInto_Throws()
    {
        Action act = () => ParseInsert("INSERT users VALUES (1)");
        act.Should().Throw<ParseException>()
           .WithMessage("*INTO*");
    }

    [Fact]
    public void Insert_MissingValues_Throws()
    {
        Action act = () => ParseInsert("INSERT INTO users (1, 2)");
        act.Should().Throw<ParseException>()
           .WithMessage("*VALUES*");
    }

    [Fact]
    public void Insert_ColumnRefInValues_Throws()
    {
        Action act = () => ParseInsert("INSERT INTO t VALUES (price * 2)");
        act.Should().Throw<ParseException>()
           .WithMessage("*literal*");
    }

    [Fact]
    public void Insert_UnclosedValuesParen_Throws()
    {
        Action act = () => ParseInsert("INSERT INTO t VALUES (1, 2");
        act.Should().Throw<ParseException>()
           .WithMessage("*')'*");
    }

    [Fact]
    public void Insert_EmptyColumnList_Throws()
    {
        Action act = () => ParseInsert("INSERT INTO t () VALUES (1)");
        act.Should().Throw<ParseException>();
    }
}