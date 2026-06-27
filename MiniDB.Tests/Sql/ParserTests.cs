using FluentAssertions;
using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Sql.Parser;

namespace MiniDB.Tests.Sql;

public class ParserTests
{
    private static SelectStatement ParseSelect(string sql) =>
        new Parser(sql).ParseSelect();


    [Fact]
    public void SelectStar_Basic()
    {
        var stmt = ParseSelect("SELECT * FROM users");

        stmt.IsSelectStar.Should().BeTrue();
        stmt.From.Should().Be("users");
        stmt.HasWhere.Should().BeFalse();
    }

    [Fact]
    public void SelectStar_CaseInsensitiveKeywords()
    {
        var stmt = ParseSelect("select * from users");
        stmt.IsSelectStar.Should().BeTrue();
        stmt.From.Should().Be("users");
    }


    [Fact]
    public void SelectColumns_SingleColumn()
    {
        var stmt = ParseSelect("SELECT age FROM users");

        stmt.Columns.Should().HaveCount(1);
        stmt.Columns[0].Expression.Should().BeOfType<ColumnRefExpr>();
        ((ColumnRefExpr)stmt.Columns[0].Expression).ColumnName.Should().Be("age");
    }

    [Fact]
    public void SelectColumns_MultipleColumns()
    {
        var stmt = ParseSelect("SELECT age, name, email FROM users");

        stmt.Columns.Should().HaveCount(3);
        ((ColumnRefExpr)stmt.Columns[0].Expression).ColumnName.Should().Be("age");
        ((ColumnRefExpr)stmt.Columns[1].Expression).ColumnName.Should().Be("name");
        ((ColumnRefExpr)stmt.Columns[2].Expression).ColumnName.Should().Be("email");
    }

    [Fact]
    public void SelectColumns_WithAlias()
    {
        var stmt = ParseSelect("SELECT age AS years FROM users");

        stmt.Columns[0].Alias.Should().Be("years");
        stmt.Columns[0].OutputName.Should().Be("years");
    }

    [Fact]
    public void SelectColumns_TableQualified()
    {
        var stmt = ParseSelect("SELECT u.name FROM users");

        var col = (ColumnRefExpr)stmt.Columns[0].Expression;
        col.ColumnName.Should().Be("name");
        col.TableName.Should().Be("u");
        col.FullName.Should().Be("u.name");
    }


    [Fact]
    public void Literal_Integer_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT 42 FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(42L);
    }

    [Fact]
    public void Literal_NegativeInteger_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT -7 FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(-7L);
    }

    [Fact]
    public void Literal_Float_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT 3.14 FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(3.14);
    }

    [Fact]
    public void Literal_String_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT 'hello' FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be("hello");
    }

    [Fact]
    public void Literal_StringWithEscapedQuote_Unescapes()
    {
        var stmt = ParseSelect("SELECT 'it''s' FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be("it's");
    }

    [Fact]
    public void Literal_True_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT TRUE FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(true);
    }

    [Fact]
    public void Literal_Null_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT NULL FROM users");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().BeNull();
    }


    [Theory]
    [InlineData("=", BinaryOp.Eq)]
    [InlineData("!=", BinaryOp.NotEq)]
    [InlineData("<>", BinaryOp.NotEq)]
    [InlineData("<", BinaryOp.Lt)]
    [InlineData("<=", BinaryOp.LtEq)]
    [InlineData(">", BinaryOp.Gt)]
    [InlineData(">=", BinaryOp.GtEq)]
    public void Where_ComparisonOperators(string op, BinaryOp expected)
    {
        var stmt = ParseSelect($"SELECT * FROM t WHERE age {op} 18");
        var where = (BinaryExpr)stmt.Where!;

        where.Op.Should().Be(expected);
        ((ColumnRefExpr)where.Left).ColumnName.Should().Be("age");
        ((LiteralExpr)where.Right).Value.Should().Be(18L);
    }


    [Fact]
    public void Where_And_ParsesAsBinaryExpr()
    {
        var stmt = ParseSelect("SELECT * FROM t WHERE age > 18 AND active = TRUE");
        var where = (BinaryExpr)stmt.Where!;

        where.Op.Should().Be(BinaryOp.And);
        var left = (BinaryExpr)where.Left;
        var right = (BinaryExpr)where.Right;
        left.Op.Should().Be(BinaryOp.Gt);
        right.Op.Should().Be(BinaryOp.Eq);
    }

    [Fact]
    public void Where_AndBindsTighterThanOr()
    {
        var stmt = ParseSelect(
            "SELECT * FROM t WHERE age > 18 AND active = TRUE OR vip = TRUE");
        var where = (BinaryExpr)stmt.Where!;

        where.Op.Should().Be(BinaryOp.Or);

        var leftAnd = (BinaryExpr)where.Left;
        leftAnd.Op.Should().Be(BinaryOp.And);

        var rightCmp = (BinaryExpr)where.Right;
        rightCmp.Op.Should().Be(BinaryOp.Eq);
    }

    [Fact]
    public void Where_MultipleAnd_LeftAssociative()
    {
        var stmt = ParseSelect("SELECT * FROM t WHERE a = 1 AND b = 2 AND c = 3");
        var top = (BinaryExpr)stmt.Where!;

        top.Op.Should().Be(BinaryOp.And);
        var topLeft = (BinaryExpr)top.Left;
        topLeft.Op.Should().Be(BinaryOp.And);
    }

    [Fact]
    public void Where_ParenthesesOverridePrecedence()
    {
        var stmt = ParseSelect(
            "SELECT * FROM t WHERE age > 18 AND (active = TRUE OR vip = TRUE)");
        var where = (BinaryExpr)stmt.Where!;

        where.Op.Should().Be(BinaryOp.And);
        var right = (BinaryExpr)where.Right;
        right.Op.Should().Be(BinaryOp.Or);
    }


    [Fact]
    public void FullQuery_ColumnsWhereAndAlias()
    {
        var stmt = ParseSelect(
            "SELECT age AS years, name FROM users WHERE age >= 21 AND name != 'Bob'");

        stmt.Columns.Should().HaveCount(2);
        stmt.Columns[0].Alias.Should().Be("years");
        stmt.From.Should().Be("users");

        var where = (BinaryExpr)stmt.Where!;
        where.Op.Should().Be(BinaryOp.And);
    }


    [Fact]
    public void MissingFrom_Throws()
    {
        Action act = () => ParseSelect("SELECT age WHERE x = 1");
        act.Should().Throw<ParseException>()
           .WithMessage("*FROM*");
    }

    [Fact]
    public void MissingTableName_Throws()
    {
        Action act = () => ParseSelect("SELECT age FROM");
        act.Should().Throw<ParseException>()
           .WithMessage("*table name*");
    }

    [Fact]
    public void TrailingGarbage_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM users EXTRA");
        act.Should().Throw<ParseException>()
           .WithMessage("*Unexpected trailing*");
    }

    [Fact]
    public void UnclosedParen_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM t WHERE (age > 18");
        act.Should().Throw<ParseException>()
           .WithMessage("*')'*");
    }

    [Fact]
    public void MissingExpression_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM t WHERE age >");
        act.Should().Throw<ParseException>()
           .WithMessage("*expression*");
    }

    [Fact]
    public void EmptySelectList_Throws()
    {
        Action act = () => ParseSelect("SELECT FROM users");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void MissingComma_Throws()
    {
        Action act = () => ParseSelect("SELECT age name FROM users");
        act.Should().Throw<ParseException>();
    }
}