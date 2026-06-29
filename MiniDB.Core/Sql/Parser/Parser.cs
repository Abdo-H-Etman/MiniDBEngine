using MiniDB.Core.Sql.Ast;
using MiniDB.Core.Sql.Lexer;

namespace MiniDB.Core.Sql.Parser;

public sealed class Parser
{
    private readonly string _source;
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(string sql)
    {
        _source = sql;
        var lexer = new Lexer.Lexer(sql.AsSpan());
        _tokens = TokenizeAll(ref lexer);
        _pos = 0;
    }

    private static List<Token> TokenizeAll(ref Lexer.Lexer lexer)
    {
        var tokens = new List<Token>();
        while (true)
        {
            var t = lexer.Next();
            tokens.Add(t);
            if (t.IsEof)
                break;
        }
        return tokens;
    }

    public SelectStatement ParseSelect()
    {
        var stmt = ParseSelectStatement();
        ExpectEof();
        return stmt;
    }

    public InsertStatement ParseInsert()
    {
        var stmt = ParseInsertStatement();
        ExpectEof();
        return stmt;
    }

    public Statement ParseStatement()
    {
        Statement stmt = _current.Kind switch
        {
            TokenKind.Select => ParseSelectStatement(),
            TokenKind.Insert => ParseInsertStatement(),
            _ => throw Error("Expected SELECT or INSERT", _current)
        };
        ExpectEof();
        return stmt;
    }
    private InsertStatement ParseInsertStatement()
    {
        Expect(TokenKind.Insert, "Expected INSERT");
        Expect(TokenKind.Into, "Expected INTO after INSERT");

        string table = ExpectIdentifierText("Expected table name sfter INTO");

        string[] columns = [];
        if (Check(TokenKind.LParen) && PeekNext().Kind == TokenKind.Identifier)
            columns = ParseIdentifierList();

        Expect(TokenKind.Values, "Expected VALUES after table name/column list");

        var rows = new List<LiteralExpr[]> { ParseValueRow() };
        while (Match(TokenKind.Comma))
            rows.Add(ParseValueRow());

        return new InsertStatement(table, columns, [.. rows]);
    }

    private string[] ParseIdentifierList()
    {
        Expect(TokenKind.LParen, "Expected '(' to start column list");

        var names = new List<string> { ExpectIdentifierText("Expected column name") };
        while (Match(TokenKind.Comma))
            names.Add(ExpectIdentifierText("Expected column name"));

        Expect(TokenKind.RParen, "Expected ')' to close column list");
        return [.. names];
    }

    private LiteralExpr[] ParseValueRow()
    {
        Expect(TokenKind.LParen, "Expected '(' to start a VALUES row");

        var values = new List<LiteralExpr> { ParseLiteral() };
        while (Match(TokenKind.Comma))
            values.Add(ParseLiteral());

        Expect(TokenKind.RParen, "Expected ')' to close VALUES row");
        return [.. values];
    }

    private LiteralExpr ParseLiteral()
    {
        var token = _current;

        switch (token.Kind)
        {
            case TokenKind.Integer:
                Advance();
                return LiteralExpr.Integer(ParseIntegerText(token));
            case TokenKind.Float:
                Advance();
                return LiteralExpr.Float(double.Parse(token.Slice(_source.AsSpan())));
            case TokenKind.String:
                Advance();
                return LiteralExpr.String(UnquoteString(token));
            case TokenKind.True:
                Advance();
                return LiteralExpr.Bool(true);
            case TokenKind.False:
                Advance();
                return LiteralExpr.Bool(false);
            case TokenKind.Null:
                Advance();
                return LiteralExpr.Null();

            default:
                throw Error("Expected literal value in VALUES list", token);
        }
    }
    private SelectStatement ParseSelectStatement()
    {
        Expect(TokenKind.Select, "Expected SELECT");

        ColumnExpr[] columns = ParseSelectList();

        Expect(TokenKind.From, "Expected FROM after select list");
        string from = ExpectIdentifierText("Expected table name after FROM");

        Expr? where = null;
        if (Match(TokenKind.Where))
            where = ParseExpr();

        return new SelectStatement(columns, from, where);
    }

    private ColumnExpr[] ParseSelectList()
    {
        if (Check(TokenKind.Star))
        {
            Advance();
            return [ColumnExpr.Star()];
        }

        var cols = new List<ColumnExpr> { ParseColumn() };

        while (Match(TokenKind.Comma))
            cols.Add(ParseColumn());

        return [.. cols];
    }

    private ColumnExpr ParseColumn()
    {
        Expr expr = ParseExpr();

        string? alias = null;
        if (Match(TokenKind.As))
            alias = ExpectIdentifierText("Expected alias name after AS");

        return ColumnExpr.Named(expr, alias);
    }

    private Expr ParseExpr() => ParseOr();

    private Expr ParseOr()
    {
        Expr left = ParseAnd();

        while (Match(TokenKind.Or))
        {
            Expr right = ParseAnd();
            left = new BinaryExpr(left, BinaryOp.Or, right);
        }

        return left;
    }

    private Expr ParseAnd()
    {
        Expr left = ParseComparison();

        while (Match(TokenKind.And))
        {
            Expr right = ParseComparison();
            left = new BinaryExpr(left, BinaryOp.And, right);
        }

        return left;
    }

    private Expr ParseComparison()
    {
        if (Match(TokenKind.Not))
            return new UnaryExpr(UnaryOp.Not, ParseComparison());

        Expr left = ParseAdditive();

        if (Check(TokenKind.Is))
        {
            Advance();
            bool negated = Match(TokenKind.Not);
            Expect(TokenKind.Null, "Expected NULL after IS [NOT]");
            return new IsNullExpr(left, negated);
        }

        if (Check(TokenKind.Not) && PeekNext().Kind == TokenKind.In)
        {
            Advance();
            Advance();
            return ParseInList(left, negated: true);
        }
        if (Check(TokenKind.In))
        {
            Advance();
            return ParseInList(left, negated: false);
        }

        if (Check(TokenKind.Not) && PeekNext().Kind == TokenKind.Between)
        {
            Advance();
            Advance();
            return ParseBetween(left, negated: true);
        }
        if (Check(TokenKind.Between))
        {
            Advance();
            return ParseBetween(left, negated: false);
        }

        BinaryOp? op = _current.Kind switch
        {
            TokenKind.Eq => BinaryOp.Eq,
            TokenKind.NotEq => BinaryOp.NotEq,
            TokenKind.Lt => BinaryOp.Lt,
            TokenKind.LtEq => BinaryOp.LtEq,
            TokenKind.Gt => BinaryOp.Gt,
            TokenKind.GtEq => BinaryOp.GtEq,
            _ => null
        };

        if (op is null)
            return left;

        Advance();
        Expr right = ParseAdditive();
        return new BinaryExpr(left, op.Value, right);
    }



    private Expr ParseInList(Expr operand, bool negated)
    {
        Expect(TokenKind.LParen, "Expected '(' After IN");

        var values = new List<Expr> { ParseExpr() };
        while (Match(TokenKind.Comma))
            values.Add(ParseExpr());

        Expect(TokenKind.RParen, "Expected ')' to close IN list");
        return new InExpr(operand, values.ToArray(), negated);
    }

    private Expr ParseBetween(Expr operand, bool negated)
    {
        Expr low = ParseAdditive();
        Expect(TokenKind.And, "Expected AND in BETWEEN clause");
        Expr high = ParseAdditive();
        return new BetweenExpr(operand, low, high, negated);
    }

    private Expr ParseAdditive()
    {
        Expr left = ParseMultiplicative();

        while (true)
        {
            BinaryOp op;
            if (Check(TokenKind.Plus))
                op = BinaryOp.Add;
            else if (Check(TokenKind.Minus))
                op = BinaryOp.Subtract;
            else
                break;

            Advance();
            Expr right = ParseMultiplicative();
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseMultiplicative()
    {
        Expr left = ParseUnary();

        while (true)
        {
            BinaryOp op;
            if (Check(TokenKind.Star))
                op = BinaryOp.Multiply;
            else if (Check(TokenKind.Slash))
                op = BinaryOp.Divide;
            else if (Check(TokenKind.Percent))
                op = BinaryOp.Modulo;
            else
                break;

            Advance();
            Expr right = ParseUnary();
            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseUnary()
    {
        if (Match(TokenKind.Minus))
            return new UnaryExpr(UnaryOp.Negate, ParseUnary());

        return ParsePrimary();
    }

    private Expr ParsePrimary()
    {
        var tok = _current;

        switch (tok.Kind)
        {
            case TokenKind.Integer:
                Advance();
                return LiteralExpr.Integer(ParseIntegerText(tok));

            case TokenKind.Float:
                Advance();
                return LiteralExpr.Float(double.Parse(tok.Slice(_source.AsSpan())));

            case TokenKind.String:
                Advance();
                return LiteralExpr.String(UnquoteString(tok));

            case TokenKind.True:
                Advance();
                return LiteralExpr.Bool(true);

            case TokenKind.False:
                Advance();
                return LiteralExpr.Bool(false);

            case TokenKind.Null:
                Advance();
                return LiteralExpr.Null();

            case TokenKind.Identifier:
                return ParseColumnRef();

            case TokenKind.LParen:
                Advance();
                Expr inner = ParseExpr();
                Expect(TokenKind.RParen, "Expected ')' to close expression");
                return inner;

            default:
                throw Error("Expected an expression", tok);
        }
    }

    private Expr ParseColumnRef()
    {
        string first = ExpectIdentifierText("Expected Identifier");

        if (Match(TokenKind.Dot))
        {
            string second = ExpectIdentifierText("Expected column name after '.'");
            return new ColumnRefExpr(second, tableName: first);
        }

        return new ColumnRefExpr(first);
    }

    private long ParseIntegerText(Token token) =>
        long.Parse(token.Slice(_source.AsSpan()));

    private string UnquoteString(Token token)
    {
        var span = token.Slice(_source.AsSpan());
        var inner = span[1..^1];

        if (!inner.Contains('\''))
            return new string(inner);

        return inner.ToString().Replace("''", "'");
    }

    private Token _current => _tokens[_pos];
    private bool Check(TokenKind kind) => _current.Kind == kind;
    private void Advance() => _pos++;

    private Token PeekNext() =>
        _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : _tokens[^1];

    private bool Match(TokenKind kind)
    {
        if (!Check(kind))
            return false;
        Advance();
        return true;
    }

    private Token Expect(TokenKind kind, string message)
    {
        if (!Check(kind))
            throw Error(message, _current);

        var token = _current;
        Advance();
        return token;
    }

    private string ExpectIdentifierText(string message)
    {
        var token = Expect(TokenKind.Identifier, message);
        return token.Text(_source.AsSpan());
    }

    private void ExpectEof()
    {
        if (!Check(TokenKind.Eof))
            throw Error("Unexpected trailing input after statement", _current);
    }

    private ParseException Error(string message, Token token) =>
        new(message, token, _source.AsSpan());
}