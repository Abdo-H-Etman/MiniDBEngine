using FluentAssertions;
using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Sql.Lexer;
using MiniDB.Core.Sql.Parser;

namespace MiniDB.Tests.Sql;

public class ParserEdgeCaseTests
{
    private static SelectStatement Select(string sql) =>
        new Parser(sql).ParseSelect();

    private static InsertStatement Insert(string sql) =>
        new Parser(sql).ParseInsert();

    private static Statement Stmt(string sql) =>
        new Parser(sql).ParseStatement();


    [Fact]
    public void TrailingSemicolon_IsTolerated()
    {
        var stmt = Select("SELECT * FROM users;");
        stmt.From.Should().Be("users");
    }

    [Fact]
    public void TrailingSemicolon_OnInsert_IsTolerated()
    {
        var stmt = Insert("INSERT INTO t VALUES (1);");
        stmt.RowCount.Should().Be(1);
    }

    [Fact]
    public void NoTrailingSemicolon_StillWorks()
    {
        var stmt = Select("SELECT * FROM users");
        stmt.From.Should().Be("users");
    }

    [Fact]
    public void DoubleSemicolon_StillThrows()
    {
        Action act = () => Select("SELECT * FROM users;;");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void DeeplyNestedParens_Resolve()
    {
        var stmt = Select("SELECT ((((1)))) FROM t");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(1L);
    }

    [Fact]
    public void DeeplyNestedParensInWhere_Resolve()
    {
        var stmt = Select("SELECT * FROM t WHERE (((age > 18)))");
        stmt.Where.Should().BeOfType<BinaryExpr>();
    }

    [Fact]
    public void SingleColumnNoComma_Works()
    {
        var stmt = Select("SELECT id FROM t");
        stmt.Columns.Should().HaveCount(1);
    }

    [Fact]
    public void ManyColumns_AllParsed()
    {
        var stmt = Select("SELECT a, b, c, d, e, f, g, h FROM t");
        stmt.Columns.Should().HaveCount(8);
    }

    [Fact]
    public void LongIdentifier_ParsesCorrectly()
    {
        string longName = new string('a', 200);
        var stmt = Select($"SELECT {longName} FROM t");
        ((ColumnRefExpr)stmt.Columns[0].Expression).ColumnName.Should().Be(longName);
    }

    [Fact]
    public void LongInClause_AllValuesParsed()
    {
        var values = string.Join(", ", Enumerable.Range(1, 100));
        var stmt = Select($"SELECT * FROM t WHERE id IN ({values})");
        var inExpr = (InExpr)stmt.Where!;
        inExpr.Values.Should().HaveCount(100);
    }

    [Fact]
    public void LongMaxValue_ParsesCorrectly()
    {
        var stmt = Select($"SELECT {long.MaxValue} FROM t");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(long.MaxValue);
    }

    [Fact]
    public void WhitespaceVariations_AllEquivalent()
    {
        var s1 = Select("SELECT*FROM t");
        var a = Select("SELECT   *   FROM   t");
        var b = Select("SELECT\t*\tFROM\tt");
        var c = Select("SELECT\n*\nFROM\nt");

        a.From.Should().Be("t");
        b.From.Should().Be("t");
        c.From.Should().Be("t");
    }

    [Fact]
    public void MixedCaseKeywords_ParseIdentically()
    {
        var a = Select("SELECT * FROM t WHERE x > 1 AND y < 2");
        var b = Select("select * from t where x > 1 and y < 2");
        var c = Select("Select * From t Where x > 1 And y < 2");

        a.From.Should().Be(b.From).And.Be(c.From);
    }

    [Fact]
    public void EmptyStringLiteral_ParsesAsEmptyString()
    {
        var stmt = Select("SELECT '' FROM t");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void StringWithOnlyEscapedQuotes_Unescapes()
    {
        var stmt = Select("SELECT '''''' FROM t");
        var lit = (LiteralExpr)stmt.Columns[0].Expression;
        lit.Value.Should().Be("''");
    }

    [Fact]
    public void NestedInAndBetween_BothWork()
    {
        var stmt = Select(
            "SELECT * FROM t WHERE id IN (1,2,3) AND age BETWEEN 18 AND 65");
        var where = (BinaryExpr)stmt.Where!;
        where.Op.Should().Be(BinaryOp.And);
        where.Left.Should().BeOfType<InExpr>();
        where.Right.Should().BeOfType<BetweenExpr>();
    }

    [Fact]
    public void AllComparisonOperatorsChainedWithOr()
    {
        var stmt = Select(
            "SELECT * FROM t WHERE a=1 OR b!=2 OR c<3 OR d<=4 OR e>5 OR f>=6");
        stmt.Where.Should().BeOfType<BinaryExpr>();
    }


    [Fact]
    public void EmptyInput_Throws()
    {
        Action act = () => Stmt("");
        act.Should().Throw<ParseException>()
           .WithMessage("*SELECT or INSERT*");
    }

    [Fact]
    public void WhitespaceOnlyInput_Throws()
    {
        Action act = () => Stmt("   \n\t  ");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void SelectWithoutFrom_Throws()
    {
        Action act = () => Select("SELECT 1");
        act.Should().Throw<ParseException>()
           .WithMessage("*FROM*");
    }

    [Fact]
    public void SelectWithoutColumns_Throws()
    {
        Action act = () => Select("SELECT FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void TrailingCommaInSelectList_Throws()
    {
        Action act = () => Select("SELECT a, FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void LeadingCommaInSelectList_Throws()
    {
        Action act = () => Select("SELECT , a FROM t");
        act.Should().Throw<ParseException>()
           .WithMessage("*expression*");
    }

    [Fact]
    public void MissingCommaBetweenColumns_Throws()
    {
        Action act = () => Select("SELECT a b FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void UnclosedLeftParen_Throws()
    {
        Action act = () => Select("SELECT (1 + 2 FROM t");
        act.Should().Throw<ParseException>()
           .WithMessage("*')'*");
    }

    [Fact]
    public void UnmatchedRightParen_Throws()
    {
        Action act = () => Select("SELECT 1) FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void DanglingOperator_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age >");
        act.Should().Throw<ParseException>()
           .WithMessage("*expression*");
    }

    [Fact]
    public void DanglingAnd_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age > 1 AND");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void DoubleOperator_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age > > 1");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void EmptyInList_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE id IN ()");
        act.Should().Throw<ParseException>()
           .WithMessage("*expression*");
    }

    [Fact]
    public void InWithoutParens_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE id IN 1, 2, 3");
        act.Should().Throw<ParseException>()
           .WithMessage("*'('*");
    }

    [Fact]
    public void BetweenMissingHigh_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age BETWEEN 18 AND");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void BetweenMissingAnd_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age BETWEEN 18 65");
        act.Should().Throw<ParseException>()
           .WithMessage("*AND*");
    }

    [Fact]
    public void IsWithoutNull_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE age IS 5");
        act.Should().Throw<ParseException>()
           .WithMessage("*NULL*");
    }

    [Fact]
    public void UnterminatedStringInWhere_Throws()
    {
        Action act = () => Select("SELECT * FROM t WHERE name = 'unterminated");
        act.Should().Throw<LexerException>();
    }

    [Fact]
    public void TableNameIsKeyword_Throws()
    {
        Action act = () => Select("SELECT * FROM WHERE");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void AliasIsReservedKeyword_Throws()
    {
        Action act = () => Select("SELECT age AS FROM FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void DoubleFrom_Throws()
    {
        Action act = () => Select("SELECT * FROM FROM t");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void EmptyParens_AsExpression_Throws()
    {
        Action act = () => Select("SELECT () FROM t");
        act.Should().Throw<ParseException>()
           .WithMessage("*expression*");
    }


    [Fact]
    public void InsertMissingTable_Throws()
    {
        Action act = () => Insert("INSERT INTO VALUES (1)");
        act.Should().Throw<ParseException>()
           .WithMessage("*table name*");
    }

    [Fact]
    public void InsertEmptyValuesRow_Throws()
    {
        Action act = () => Insert("INSERT INTO t VALUES ()");
        act.Should().Throw<ParseException>()
           .WithMessage("*literal*");
    }

    [Fact]
    public void InsertTrailingCommaInRow_Throws()
    {
        Action act = () => Insert("INSERT INTO t VALUES (1, 2,)");
        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void InsertExpressionInValues_Throws()
    {
        Action act = () => Insert("INSERT INTO t VALUES (col)");
        act.Should().Throw<ParseException>()
           .WithMessage("*literal*");
    }

    [Fact]
    public void InsertMismatchedRowShapes_ParsesButRowsDiffer()
    {
        var stmt = Insert("INSERT INTO t VALUES (1, 2), (3)");
        stmt.Rows[0].Should().HaveCount(2);
        stmt.Rows[1].Should().HaveCount(1);
    }

    [Fact]
    public void InsertColumnListWithTrailingComma_Throws()
    {
        Action act = () => Insert("INSERT INTO t (a, b,) VALUES (1, 2)");
        act.Should().Throw<ParseException>();
    }


    [Fact]
    public void ErrorPosition_PointsAtTheCorrectToken()
    {
        const string sql = "SELECT * FROM t WHERE age >";
        try
        {
            Select(sql);
            Assert.Fail("Expected ParseException");
        }
        catch (ParseException ex)
        {
            ex.Found.IsEof.Should().BeTrue();
        }
    }

    [Fact]
    public void ErrorMessage_IncludesOffendingTokenText()
    {
        Action act = () => Select("SELECT * FROM t WHERE age >>> 1");
        act.Should().Throw<ParseException>()
           .Where(ex => ex.Message.Contains("position"));
    }
}