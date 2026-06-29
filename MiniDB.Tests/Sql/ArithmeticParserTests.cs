using FluentAssertions;
using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Sql.Parser;

namespace MiniDB.Tests.Sql;

public class ArithmeticParserTests
{
    private static SelectStatement ParseSelect(string sql) =>
        new Parser(sql).ParseSelect();

    private static Expr Where(string sql) =>
        ParseSelect(sql).Where!;


    [Fact]
    public void Add_ParsesAsBinaryExpr()
    {
        var stmt = ParseSelect("SELECT price + tax FROM orders");
        var expr = (BinaryExpr)stmt.Columns[0].Expression;
        expr.Op.Should().Be(BinaryOp.Add);
    }

    [Fact]
    public void Subtract_ParsesAsBinaryExpr()
    {
        var stmt = ParseSelect("SELECT quantity - reserved FROM stock");
        var expr = (BinaryExpr)stmt.Columns[0].Expression;
        expr.Op.Should().Be(BinaryOp.Subtract);
    }


    [Fact]
    public void MultiplyBindsTighterThanAdd()
    {
        var stmt = ParseSelect("SELECT price * 1.1 + tax FROM orders");
        var top = (BinaryExpr)stmt.Columns[0].Expression;

        top.Op.Should().Be(BinaryOp.Add);
        var left = (BinaryExpr)top.Left;
        left.Op.Should().Be(BinaryOp.Multiply);
    }

    [Fact]
    public void DivideBindsTighterThanSubtract()
    {
        var stmt = ParseSelect("SELECT total - discount / 2 FROM orders");
        var top = (BinaryExpr)stmt.Columns[0].Expression;

        top.Op.Should().Be(BinaryOp.Subtract);
        var right = (BinaryExpr)top.Right;
        right.Op.Should().Be(BinaryOp.Divide);
    }

    [Fact]
    public void Modulo_ParsesCorrectly()
    {
        var stmt = ParseSelect("SELECT id % 2 FROM users");
        var expr = (BinaryExpr)stmt.Columns[0].Expression;
        expr.Op.Should().Be(BinaryOp.Modulo);
    }


    [Fact]
    public void ChainedSubtraction_LeftAssociative()
    {
        var stmt = ParseSelect("SELECT a - b - c FROM t");
        var top = (BinaryExpr)stmt.Columns[0].Expression;

        top.Op.Should().Be(BinaryOp.Subtract);
        var left = (BinaryExpr)top.Left;
        left.Op.Should().Be(BinaryOp.Subtract);
        ((ColumnRefExpr)left.Left).ColumnName.Should().Be("a");
    }


    [Fact]
    public void ArithmeticInsideComparison()
    {
        var where = (BinaryExpr)Where(
            "SELECT * FROM orders WHERE total > price * 0.9");

        where.Op.Should().Be(BinaryOp.Gt);
        var right = (BinaryExpr)where.Right;
        right.Op.Should().Be(BinaryOp.Multiply);
    }

    [Fact]
    public void ArithmeticBothSidesOfComparison()
    {
        var where = (BinaryExpr)Where(
            "SELECT * FROM stock WHERE quantity - reserved > 0");

        where.Op.Should().Be(BinaryOp.Gt);
        var left = (BinaryExpr)where.Left;
        left.Op.Should().Be(BinaryOp.Subtract);
    }


    [Fact]
    public void UnaryMinus_OnColumn()
    {
        var stmt = ParseSelect("SELECT -balance FROM accounts");
        var expr = (UnaryExpr)stmt.Columns[0].Expression;
        expr.Op.Should().Be(UnaryOp.Negate);
        ((ColumnRefExpr)expr.Operand).ColumnName.Should().Be("balance");
    }

    [Fact]
    public void UnaryNot_OnComparison()
    {
        var where = (UnaryExpr)Where("SELECT * FROM t WHERE NOT active = TRUE");
        where.Op.Should().Be(UnaryOp.Not);
    }


    [Fact]
    public void ParenthesesOverrideArithmeticPrecedence()
    {
        var stmt = ParseSelect("SELECT (price + tax) * quantity FROM orders");
        var top = (BinaryExpr)stmt.Columns[0].Expression;

        top.Op.Should().Be(BinaryOp.Multiply);
        var left = (BinaryExpr)top.Left;
        left.Op.Should().Be(BinaryOp.Add);
    }


    [Fact]
    public void IsNull_ParsesCorrectly()
    {
        var where = (IsNullExpr)Where("SELECT * FROM t WHERE email IS NULL");
        where.Negated.Should().BeFalse();
        ((ColumnRefExpr)where.Operand).ColumnName.Should().Be("email");
    }

    [Fact]
    public void IsNotNull_ParsesCorrectly()
    {
        var where = (IsNullExpr)Where("SELECT * FROM t WHERE email IS NOT NULL");
        where.Negated.Should().BeTrue();
    }


    [Fact]
    public void In_ParsesCorrectly()
    {
        var where = (InExpr)Where("SELECT * FROM t WHERE status IN (1, 2, 3)");
        where.Negated.Should().BeFalse();
        where.Values.Should().HaveCount(3);
    }

    [Fact]
    public void NotIn_ParsesCorrectly()
    {
        var where = (InExpr)Where(
            "SELECT * FROM t WHERE role NOT IN ('admin', 'super')");
        where.Negated.Should().BeTrue();
        where.Values.Should().HaveCount(2);
    }


    [Fact]
    public void Between_ParsesCorrectly()
    {
        var where = (BetweenExpr)Where(
            "SELECT * FROM t WHERE age BETWEEN 18 AND 65");
        where.Negated.Should().BeFalse();
        ((LiteralExpr)where.Low).Value.Should().Be(18L);
        ((LiteralExpr)where.High).Value.Should().Be(65L);
    }

    [Fact]
    public void NotBetween_ParsesCorrectly()
    {
        var where = (BetweenExpr)Where(
            "SELECT * FROM t WHERE age NOT BETWEEN 0 AND 17");
        where.Negated.Should().BeTrue();
    }

    [Fact]
    public void Between_WithArithmeticBounds()
    {
        var where = (BetweenExpr)Where(
            "SELECT * FROM t WHERE price BETWEEN min_price * 0.9 AND max_price * 1.1");

        where.Low.Should().BeOfType<BinaryExpr>();
        where.High.Should().BeOfType<BinaryExpr>();
    }


    [Fact]
    public void IsWithoutNull_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM t WHERE x IS 5");
        act.Should().Throw<ParseException>()
           .WithMessage("*NULL*");
    }

    [Fact]
    public void BetweenWithoutAnd_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM t WHERE x BETWEEN 1 5");
        act.Should().Throw<ParseException>()
           .WithMessage("*AND*");
    }

    [Fact]
    public void InWithoutClosingParen_Throws()
    {
        Action act = () => ParseSelect("SELECT * FROM t WHERE x IN (1, 2");
        act.Should().Throw<ParseException>()
           .WithMessage("*')'*");
    }
}