using FluentAssertions;
using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Types;

namespace MiniDB.Tests.Sql;

public class AstTests
{
    [Fact]
    public void LiteralExpr_Integer_HasCorrectTypeAndValue()
    {
        var lit = LiteralExpr.Integer(42L);
        lit.Type.Should().Be(DbType.Int64);
        lit.Value.Should().Be(42L);
    }

    [Fact]
    public void LiteralExpr_Float_HasCorrectTypeAndValue()
    {
        var lit = LiteralExpr.Float(3.14);
        lit.Type.Should().Be(DbType.Float64);
        lit.Value.Should().Be(3.14);
    }

    [Fact]
    public void LiteralExpr_String_HasCorrectTypeAndValue()
    {
        var lit = LiteralExpr.String("hello");
        lit.Type.Should().Be(DbType.Text);
        lit.Value.Should().Be("hello");
    }

    [Fact]
    public void LiteralExpr_Bool_HasCorrectTypeAndValue()
    {
        LiteralExpr.Bool(true).Value.Should().Be(true);
        LiteralExpr.Bool(false).Value.Should().Be(false);
    }

    [Fact]
    public void LiteralExpr_Null_HasNullValue()
    {
        var lit = LiteralExpr.Null();
        lit.Type.Should().Be(DbType.Null);
        lit.Value.Should().BeNull();
        lit.ToString().Should().Be("NULL");
    }


    [Fact]
    public void ColumnRefExpr_Unqualified_FullNameIsColumnName()
    {
        var col = new ColumnRefExpr("age");
        col.ColumnName.Should().Be("age");
        col.TableName.Should().BeNull();
        col.FullName.Should().Be("age");
    }

    [Fact]
    public void ColumnRefExpr_Qualified_FullNameIncludesTable()
    {
        var col = new ColumnRefExpr("name", "users");
        col.FullName.Should().Be("users.name");
        col.TableName.Should().Be("users");
    }


    [Fact]
    public void BinaryExpr_ToStringShowsOperands()
    {
        var expr = new BinaryExpr(
            new ColumnRefExpr("age"),
            BinaryOp.Gt,
            LiteralExpr.Integer(25L));

        expr.Op.Should().Be(BinaryOp.Gt);
        expr.ToString().Should().Be("age Gt 25");
    }

    [Fact]
    public void BinaryExpr_Nested_TreeIsCorrect()
    {
        var left = new BinaryExpr(
            new ColumnRefExpr("age"), BinaryOp.Gt, LiteralExpr.Integer(18L));
        var right = new BinaryExpr(
            new ColumnRefExpr("name"), BinaryOp.Eq, LiteralExpr.String("Ali"));
        var and = new BinaryExpr(left, BinaryOp.And, right);

        and.Op.Should().Be(BinaryOp.And);
        ((BinaryExpr)and.Left).Op.Should().Be(BinaryOp.Gt);
        ((BinaryExpr)and.Right).Op.Should().Be(BinaryOp.Eq);
    }


    [Fact]
    public void UnaryExpr_Negate_ToString()
    {
        var expr = new UnaryExpr(UnaryOp.Negate, new ColumnRefExpr("price"));
        expr.ToString().Should().Be("Negate price");
    }

    [Fact]
    public void UnaryExpr_Not_ToString()
    {
        var expr = new UnaryExpr(UnaryOp.Not, new ColumnRefExpr("active"));
        expr.ToString().Should().Be("Not active");
    }


    [Fact]
    public void IsNullExpr_IsNull_ToString()
    {
        var expr = new IsNullExpr(new ColumnRefExpr("email"));
        expr.Negated.Should().BeFalse();
        expr.ToString().Should().Be("email IS NULL");
    }

    [Fact]
    public void IsNullExpr_IsNotNull_ToString()
    {
        var expr = new IsNullExpr(new ColumnRefExpr("email"), negated: true);
        expr.Negated.Should().BeTrue();
        expr.ToString().Should().Be("email IS NOT NULL");
    }


    [Fact]
    public void InExpr_BasicInList()
    {
        var expr = new InExpr(
            new ColumnRefExpr("status"),
            [LiteralExpr.Integer(1L), LiteralExpr.Integer(2L), LiteralExpr.Integer(3L)]);

        expr.Negated.Should().BeFalse();
        expr.Values.Should().HaveCount(3);
        expr.ToString().Should().Be("(status IN (1, 2, 3))");
    }

    [Fact]
    public void InExpr_NotIn_ToString()
    {
        var expr = new InExpr(
            new ColumnRefExpr("role"),
            [LiteralExpr.String("admin"), LiteralExpr.String("superuser")],
            negated: true);

        expr.Negated.Should().BeTrue();
        expr.ToString().Should().Contain("NOT IN");
    }


    [Fact]
    public void BetweenExpr_ToString()
    {
        var expr = new BetweenExpr(
            new ColumnRefExpr("age"),
            LiteralExpr.Integer(18L),
            LiteralExpr.Integer(65L));

        expr.Negated.Should().BeFalse();
        expr.ToString().Should().Be("(age BETWEEN 18 AND 65)");
    }

    [Fact]
    public void BetweenExpr_Negated_ToString()
    {
        var expr = new BetweenExpr(
            new ColumnRefExpr("age"),
            LiteralExpr.Integer(18L),
            LiteralExpr.Integer(65L),
            negated: true);

        expr.ToString().Should().Contain("NOT BETWEEN");
    }


    [Fact]
    public void ColumnExpr_Star_IsStar()
    {
        var col = ColumnExpr.Star();
        col.IsStar.Should().BeTrue();
        col.ToString().Should().Be("*");
    }

    [Fact]
    public void ColumnExpr_Named_OutputNameIsColumnName()
    {
        var col = ColumnExpr.Named(new ColumnRefExpr("age"));
        col.IsStar.Should().BeFalse();
        col.OutputName.Should().Be("age");
        col.Alias.Should().BeNull();
    }

    [Fact]
    public void ColumnExpr_WithAlias_OutputNameIsAlias()
    {
        var col = ColumnExpr.Named(
            new ColumnRefExpr("price"),
            alias: "unit_price");

        col.OutputName.Should().Be("unit_price");
        col.Alias.Should().Be("unit_price");
        col.ToString().Should().Be("price AS unit_price");
    }

    [Fact]
    public void ColumnExpr_ComputedExpr_OutputNameIsFallback()
    {
        var col = ColumnExpr.Named(
            new BinaryExpr(
                new ColumnRefExpr("price"),
                BinaryOp.Multiply,
                LiteralExpr.Float(1.1)));

        col.OutputName.Should().Be("?");
    }


    [Fact]
    public void OrderByClause_DefaultIsAsc()
    {
        var ob = new OrderByClause(new ColumnRefExpr("age"));
        ob.Direction.Should().Be(SortDirection.Asc);
        ob.ToString().Should().Be("age ASC");
    }

    [Fact]
    public void OrderByClause_Desc_ToString()
    {
        var ob = new OrderByClause(
            new ColumnRefExpr("created_at"),
            SortDirection.Desc);
        ob.ToString().Should().Be("created_at DESC");
    }


    [Fact]
    public void SelectStatement_MinimalSelectStar()
    {
        var stmt = new SelectStatement(
            columns: [ColumnExpr.Star()],
            from: "users");

        stmt.IsSelectStar.Should().BeTrue();
        stmt.HasWhere.Should().BeFalse();
        stmt.HasOrderBy.Should().BeFalse();
        stmt.HasLimit.Should().BeFalse();
        stmt.From.Should().Be("users");
        stmt.ToString().Should().Be("SELECT * FROM users");
    }

    [Fact]
    public void SelectStatement_WithWhere_HasWhere()
    {
        var stmt = new SelectStatement(
            columns: [ColumnExpr.Named(new ColumnRefExpr("age"))],
            from: "users",
            where: new BinaryExpr(
                new ColumnRefExpr("age"),
                BinaryOp.Gt,
                LiteralExpr.Integer(18L)));

        stmt.HasWhere.Should().BeTrue();
        stmt.Where.Should().BeOfType<BinaryExpr>();
        stmt.ToString().Should().Contain("WHERE");
    }

    [Fact]
    public void SelectStatement_WithOrderByAndLimit()
    {
        var stmt = new SelectStatement(
            columns: [ColumnExpr.Star()],
            from: "users",
            orderBy: [new OrderByClause(new ColumnRefExpr("age"), SortDirection.Desc)],
            limit: 10,
            offset: 20);

        stmt.HasOrderBy.Should().BeTrue();
        stmt.HasLimit.Should().BeTrue();
        stmt.Limit.Should().Be(10);
        stmt.Offset.Should().Be(20);
        stmt.ToString().Should().Contain("ORDER BY")
            .And.Contain("LIMIT 10")
            .And.Contain("OFFSET 20");
    }

    [Fact]
    public void SelectStatement_EmptyColumns_Throws()
    {
        Action act = () => new SelectStatement(
            columns: [],
            from: "users");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*at least one column*");
    }


    [Fact]
    public void InsertStatement_BasicInsert()
    {
        var stmt = new InsertStatement(
            table: "users",
            columns: ["id", "name"],
            rows: [[LiteralExpr.Integer(1L), LiteralExpr.String("Ali")]]);

        stmt.Table.Should().Be("users");
        stmt.HasColumnList.Should().BeTrue();
        stmt.RowCount.Should().Be(1);
        stmt.ToString().Should().Be("INSERT INTO users (id, name) VALUES (1, Ali)");
    }

    [Fact]
    public void InsertStatement_NoColumnList()
    {
        var stmt = new InsertStatement(
            table: "users",
            columns: [],
            rows: [[LiteralExpr.Integer(42L)]]);

        stmt.HasColumnList.Should().BeFalse();
        stmt.ToString().Should().Be("INSERT INTO users VALUES (42)");
    }

    [Fact]
    public void InsertStatement_MultiRow_RowCountIsCorrect()
    {
        var stmt = new InsertStatement(
            table: "users",
            columns: ["id"],
            rows:
            [
                [LiteralExpr.Integer(1L)],
                [LiteralExpr.Integer(2L)],
                [LiteralExpr.Integer(3L)]
            ]);

        stmt.RowCount.Should().Be(3);
    }

    [Fact]
    public void InsertStatement_EmptyRows_Throws()
    {
        Action act = () => new InsertStatement(
            table: "users",
            columns: [],
            rows: []);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*at least one row*");
    }


    [Fact]
    public void Statement_PatternMatch_DispatchesCorrectly()
    {
        Statement stmt = new SelectStatement(
            [ColumnExpr.Star()], "users");

        string result = stmt switch
        {
            SelectStatement s => $"SELECT from {s.From}",
            InsertStatement i => $"INSERT into {i.Table}",
            _ => "unknown"
        };

        result.Should().Be("SELECT from users");
    }

    [Fact]
    public void Expr_PatternMatch_DispatchesCorrectly()
    {
        Expr expr = new BinaryExpr(
            new ColumnRefExpr("age"),
            BinaryOp.Gt,
            LiteralExpr.Integer(18L));

        string result = expr switch
        {
            BinaryExpr b => $"binary:{b.Op}",
            LiteralExpr l => $"literal:{l.Value}",
            ColumnRefExpr c => $"col:{c.ColumnName}",
            _ => "other"
        };

        result.Should().Be("binary:Gt");
    }
}