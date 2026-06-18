using FluentAssertions;
using MiniDB.Core.Sql.Lexer;

namespace MiniDB.Tests.Sql;

public class LexerTests
{
    private static List<Token> Lex(string sql)
    {
        var tokens = new List<Token>();
        var lexer = new Lexer(sql.AsSpan());
        while (true)
        {
            var t = lexer.Next();
            tokens.Add(t);
            if (t.IsEof) break;
        }
        return tokens;
    }

    private static List<TokenKind> Kinds(string sql) =>
        [.. Lex(sql).Select(q => q.Kind)];

    [Fact]
    public void EmptyString_ReturnsEofOnly()
    {
        var tokens = Lex("");
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.Eof);
    }

    [Fact]
    public void WhitespaceOnly_ReturnsEofOnly()
    {
        var tokens = Lex("   \t\n   ");
        tokens.Should().HaveCount(1);
        tokens[0].Kind.Should().Be(TokenKind.Eof);
    }


    [Theory]
    [InlineData("SELECT", TokenKind.Select)]
    [InlineData("FROM", TokenKind.From)]
    [InlineData("WHERE", TokenKind.Where)]
    [InlineData("INSERT", TokenKind.Insert)]
    [InlineData("INTO", TokenKind.Into)]
    [InlineData("VALUES", TokenKind.Values)]
    [InlineData("AND", TokenKind.And)]
    [InlineData("OR", TokenKind.Or)]
    [InlineData("NOT", TokenKind.Not)]
    [InlineData("NULL", TokenKind.Null)]
    [InlineData("TRUE", TokenKind.True)]
    [InlineData("FALSE", TokenKind.False)]
    [InlineData("IS", TokenKind.Is)]
    [InlineData("IN", TokenKind.In)]
    [InlineData("LIKE", TokenKind.Like)]
    [InlineData("BETWEEN", TokenKind.Between)]
    [InlineData("ORDER", TokenKind.Order)]
    [InlineData("BY", TokenKind.By)]
    [InlineData("ASC", TokenKind.Asc)]
    [InlineData("DESC", TokenKind.Desc)]
    [InlineData("LIMIT", TokenKind.Limit)]
    [InlineData("OFFSET", TokenKind.Offset)]
    [InlineData("AS", TokenKind.As)]
    public void Keyword_RecognizedCaseInsensitive(string word, TokenKind expected)
    {
        Kinds(word).Should().StartWith(expected);
        Kinds(word.ToLower()).Should().StartWith(expected);
        Kinds(char.ToLower(word[0]) + word[1..]);
    }

    [Theory]
    [InlineData("users")]
    [InlineData("user_id")]
    [InlineData("_private")]
    [InlineData("camelCase")]
    [InlineData("col1")]
    public void Identifier_RecognizedCorrectly(string ident)
    {
        var tokens = Lex(ident);
        tokens[0].Kind.Should().Be(TokenKind.Identifier);
        tokens[0].Text(ident.AsSpan()).Should().Be(ident);
    }

    [Fact]
    public void Identifier_DoesNotMatchKeyword() =>
        Kinds("Selecting").Should().StartWith(TokenKind.Identifier);

    [Theory]
    [InlineData("0")]
    [InlineData("99999")]
    [InlineData("-10")]
    [InlineData("-90")]
    public void Integer_RecognizedCorrectly(string input)
    {
        var tokens = Lex(input);
        tokens[0].Kind.Should().Be(TokenKind.Integer);
        var x = tokens[0].Text(input);
        tokens[0].Text(input.AsSpan()).Should().Be(input);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("999.99")]
    [InlineData("-10.3")]
    [InlineData("-90.001")]
    public void Float_RecognizedCorrectly(string input)
    {
        var tokens = Lex(input);
        tokens[0].Kind.Should().Be(TokenKind.Float);
        tokens[0].Text(input.AsSpan()).Should().Be(input);
    }

    [Fact]
    public void DotWithoutDigitAfter_IsNotFloat()
    {
        var kinds = Kinds("3.");
        kinds.Should().StartWith([TokenKind.Integer, TokenKind.Dot]);
    }

    [Fact]
    public void StringLiteral_BasicRoundTrip()
    {
        var tokens = Lex("'hello'");
        tokens[0].Kind.Should().Be(TokenKind.String);
        tokens[0].Text("'hello'".AsSpan()).Should().Be("'hello'");
    }

    [Fact]
    public void StringLiteral_EmptyString()
    {
        var tokens = Lex("''");
        tokens[0].Kind.Should().Be(TokenKind.String);
        tokens[0].Length.Should().Be(2);
    }

    [Fact]
    public void StringLiteral_EscapedQuote()
    {
        var tokens = Lex("'it''s'");
        tokens[0].Kind.Should().Be(TokenKind.String);
        tokens[0].Length.Should().Be(7);
    }

    [Fact]
    public void StringLiteral_Unterminated_Throws()
    {
        Action act = () => Lex("'unterminated");
        act.Should().Throw<LexerException>()
           .WithMessage("*Unterminated string*");
    }

    [Theory]
    [InlineData("=", TokenKind.Eq)]
    [InlineData("!=", TokenKind.NotEq)]
    [InlineData("<>", TokenKind.NotEq)]
    [InlineData("<", TokenKind.Lt)]
    [InlineData("<=", TokenKind.LtEq)]
    [InlineData(">", TokenKind.Gt)]
    [InlineData(">=", TokenKind.GtEq)]
    [InlineData("+", TokenKind.Plus)]
    [InlineData("-", TokenKind.Minus)]
    [InlineData("/", TokenKind.Slash)]
    [InlineData("%", TokenKind.Percent)]
    public void Operator_RecognizedCorrectly(string input, TokenKind expected)
    {
        Kinds(input).Should().StartWith([expected]);
    }

    [Theory]
    [InlineData("*", TokenKind.Star)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData(";", TokenKind.Semicolon)]
    [InlineData("(", TokenKind.LParen)]
    [InlineData(")", TokenKind.RParen)]
    [InlineData(".", TokenKind.Dot)]
    public void Punctuation_RecognizedCorrectly(string input, TokenKind expected)
    {
        Kinds(input).Should().StartWith([expected]);
    }

    [Fact]
    public void Token_StartAndLength_AreCorrect()
    {
        string sql = "SELECT age";
        var tokens = Lex(sql);

        tokens[0].Kind.Should().Be(TokenKind.Select);
        tokens[0].Start.Should().Be(0);
        tokens[0].Length.Should().Be(6);

        tokens[1].Kind.Should().Be(TokenKind.Identifier);
        tokens[1].Start.Should().Be(7);
        tokens[1].Length.Should().Be(3);
    }

    [Fact]
    public void Token_Slice_MatchesSourceText()
    {
        string sql = "FROM users";
        var tokens = Lex(sql);

        tokens[0].Slice(sql.AsSpan()).ToString().Should().Be("FROM");
        tokens[1].Slice(sql.AsSpan()).ToString().Should().Be("users");
    }

    [Fact]
    public void LineComment_IsSkipped()
    {
        var kinds = Kinds("SELECT -- this is a comment\nFROM");
        kinds.Should().Equal(
            TokenKind.Select,
            TokenKind.From,
            TokenKind.Eof);
    }

    [Fact]
    public void BlockComment_IsSkipped()
    {
        var kinds = Kinds("SELECT /* pick columns */ FROM");
        kinds.Should().Equal(
            TokenKind.Select,
            TokenKind.From,
            TokenKind.Eof);
    }

    [Fact]
    public void BlockComment_Unterminated_Throws()
    {
        Action act = () => Lex("SELECT /* unterminated");
        act.Should().Throw<LexerException>()
           .WithMessage("*Unterminated block comment*");
    }

    [Fact]
    public void SelectWithWhere_TokenizesCorrectly()
    {
        var kinds = Kinds("SELECT a, b FROM t WHERE a > 5");
        kinds.Should().Equal(
            TokenKind.Select,
            TokenKind.Identifier,
            TokenKind.Comma,
            TokenKind.Identifier,
            TokenKind.From,
            TokenKind.Identifier,
            TokenKind.Where,
            TokenKind.Identifier,
            TokenKind.Gt,
            TokenKind.Integer,
            TokenKind.Eof);
    }

    [Fact]
    public void InsertStatement_TokenizesCorrectly()
    {
        var kinds = Kinds("INSERT INTO users (id, name) VALUES (1, 'Ali')");
        kinds.Should().Equal(
            TokenKind.Insert,
            TokenKind.Into,
            TokenKind.Identifier,
            TokenKind.LParen,
            TokenKind.Identifier,
            TokenKind.Comma,
            TokenKind.Identifier,
            TokenKind.RParen,
            TokenKind.Values,
            TokenKind.LParen,
            TokenKind.Integer,
            TokenKind.Comma,
            TokenKind.String,
            TokenKind.RParen,
            TokenKind.Eof);
    }

    [Fact]
    public void WhereWithAndOr_TokenizesCorrectly()
    {
        var kinds = Kinds("WHERE age > 18 AND name = 'Ali' OR active = TRUE");
        kinds.Should().Equal(
            TokenKind.Where,
            TokenKind.Identifier,
            TokenKind.Gt,
            TokenKind.Integer,
            TokenKind.And,
            TokenKind.Identifier,
            TokenKind.Eq,
            TokenKind.String,
            TokenKind.Or,
            TokenKind.Identifier,
            TokenKind.Eq,
            TokenKind.True,
            TokenKind.Eof);
    }

    [Fact]
    public void NullLiteral_TokenizesCorrectly()
    {
        var kinds = Kinds("WHERE col IS NULL");
        kinds.Should().Equal(
            TokenKind.Where,
            TokenKind.Identifier,
            TokenKind.Is,
            TokenKind.Null,
            TokenKind.Eof);
    }


    [Fact]
    public void UnexpectedChar_Throws()
    {
        Action act = () => Lex("SELECT @col");
        act.Should().Throw<LexerException>()
           .WithMessage("*'@'*");
    }

    [Fact]
    public void BangWithoutEq_Throws()
    {
        Action act = () => Lex("a ! b");
        act.Should().Throw<LexerException>()
           .WithMessage("*'!'*");
    }
}