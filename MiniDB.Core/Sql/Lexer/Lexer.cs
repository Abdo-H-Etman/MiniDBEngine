using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MiniDB.Core.Sql.Lexer;

public ref struct Lexer
{
    private readonly ReadOnlySpan<char> _source;
    private int _pos;

    public Lexer(ReadOnlySpan<char> source)
    {
        _source = source;
        _pos = 0;
    }

    public Token Next()
    {
        SkipWhitespaceAndComments();

        if (_pos >= _source.Length)
            return Make(TokenKind.Eof, _pos, 0);

        char c = _source[_pos];

        return c switch
        {
            '*' => Consume(TokenKind.Star),
            ',' => Consume(TokenKind.Comma),
            ';' => Consume(TokenKind.Semicolon),
            '(' => Consume(TokenKind.LParen),
            ')' => Consume(TokenKind.RParen),
            '.' => Consume(TokenKind.Dot),
            '+' => Consume(TokenKind.Plus),
            '%' => Consume(TokenKind.Percent),

            '=' => Consume(TokenKind.Eq),
            '!' => ConsumeNotEq(),
            '<' => ConsumeltOrLtEq(),
            '>' => ConsumeGtOrGtEq(),
            '-' => ConsumeMinus(),
            '/' => ConsumeSlash(),

            '\'' => ConsumeString(),

            _ when char.IsDigit(c) => ConsumeNumber(),
            _ when IsIdentStart(c) => ConsumeIdentOrKeyWord(),

            _ => throw new LexerException(c, _pos)
        };
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (true)
        {
            var t = Next();
            tokens.Add(t);
            if (t.IsEof)
                break;
        }

        return tokens;
    }

    public int Position => _pos;
    private void SkipWhitespaceAndComments()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];

            if (char.IsWhiteSpace(c)) { _pos++; continue; }

            if (c == '-' && Peek(1) == '-')
            {
                _pos += 2;
                while (_pos < _source.Length && _source[_pos] != '\n')
                    _pos++;
                continue;
            }

            if (c == '/' && Peek(1) == '*')
            {
                int start = _pos;
                _pos += 2;
                while (_pos < _source.Length - 1 && !(_source[_pos] == '*' && _source[_pos + 1] == '/'))
                    _pos++;

                if (_pos >= _source.Length - 1)
                    throw new LexerException(
                        "Unterminated block comment", start);

                _pos += 2;
                continue;
            }

            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Token Consume(TokenKind kind)
    {
        var t = Make(kind, _pos, 1);
        _pos++;
        return t;
    }

    private Token ConsumeNotEq()
    {
        if (Peek(1) != '=')
            throw new LexerException('!', _pos);

        var t = Make(TokenKind.NotEq, _pos, 2);
        _pos++;

        return t;
    }

    private Token ConsumeltOrLtEq()
    {
        if (Peek(1) == '=')
        {
            var t = Make(TokenKind.LtEq, _pos, 2);
            _pos += 2;
            return t;
        }

        if (Peek(1) == '>')
        {
            var t = Make(TokenKind.NotEq, _pos, 2);
            _pos += 2;
            return t;
        }

        return Consume(TokenKind.Lt);
    }

    private Token ConsumeGtOrGtEq()
    {
        if (Peek(1) == '=')
        {
            var t = Make(TokenKind.GtEq, _pos, 2);
            _pos += 2;
            return t;
        }

        return Consume(TokenKind.Gt);
    }

    private Token ConsumeMinus()
    {
        if (char.IsDigit(Peek(1)))
            return ConsumeNumber();
        return Consume(TokenKind.Minus);
    }

    private Token ConsumeSlash() => Consume(TokenKind.Slash);

    private Token ConsumeString()
    {
        int start = _pos;
        _pos++;

        while (_pos < _source.Length)
        {
            char c = _source[_pos];

            if (c == '\'' && Peek(1) == '\'')
            {
                _pos += 2;
                continue;
            }

            if (c == '\'')
            {
                _pos++;
                return Make(TokenKind.String, start, _pos - start);
            }

            _pos++;
        }
        throw new LexerException("Unterminated string literal", start);
    }

    private Token ConsumeNumber()
    {
        int start = _pos;

        if (_pos < _source.Length && _source[_pos] == '-')
            _pos++;

        while (_pos < _source.Length && char.IsDigit(_source[_pos]))
            _pos++;

        if (_pos < _source.Length
            && _source[_pos] == '.'
            && _pos + 1 < _source.Length
            && char.IsDigit(_source[_pos + 1]))
        {
            _pos++;
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
                _pos++;
            return Make(TokenKind.Float, start, _pos - start);
        }

        return Make(TokenKind.Integer, start, _pos - start);
    }

    private Token ConsumeIdentOrKeyWord()
    {
        int start = _pos;

        while (_pos < _source.Length && IsIdentContinue(_source[_pos]))
            _pos++;

        int length = _pos - start;

        TokenKind kind = MatchKeyWord(_source.Slice(start, length));
        return Make(kind, start, length);
    }

    private static TokenKind MatchKeyWord(ReadOnlySpan<char> word)
    {
        return word.Length switch
        {
            2 => word switch
            {
                _ when Eq(word, "AS") => TokenKind.As,
                _ when Eq(word, "BY") => TokenKind.By,
                _ when Eq(word, "IN") => TokenKind.In,
                _ when Eq(word, "OR") => TokenKind.Or,
                _ when Eq(word, "Is") => TokenKind.Is,
                _ => TokenKind.Identifier
            },
            3 => word switch
            {
                _ when Eq(word, "AND") => TokenKind.And,
                _ when Eq(word, "DOT") => TokenKind.Dot,
                _ when Eq(word, "ASC") => TokenKind.Asc,
                _ when Eq(word, "NOT") => TokenKind.Not,
                _ => TokenKind.Identifier
            },
            4 => word switch
            {
                _ when Eq(word, "DESC") => TokenKind.Desc,
                _ when Eq(word, "INTO") => TokenKind.Into,
                _ when Eq(word, "FROM") => TokenKind.From,
                _ when Eq(word, "NULL") => TokenKind.Null,
                _ when Eq(word, "LIKE") => TokenKind.Like,
                _ when Eq(word, "TRUE") => TokenKind.True,
                _ => TokenKind.Identifier
            },
            5 => word switch
            {
                _ when Eq(word, "FALSE") => TokenKind.False,
                _ when Eq(word, "LIMIT") => TokenKind.Limit,
                _ when Eq(word, "ORDER") => TokenKind.Order,
                _ when Eq(word, "WHERE") => TokenKind.Where,
                _ => TokenKind.Identifier
            },
            6 => word switch
            {
                _ when Eq(word, "SELECT") => TokenKind.Select,
                _ when Eq(word, "VALUES") => TokenKind.Values,
                _ when Eq(word, "OFFSET") => TokenKind.Offset,
                _ when Eq(word, "INSERT") => TokenKind.Insert,
                _ => TokenKind.Identifier
            },
            7 when Eq(word, "BETWEEN") => TokenKind.Between,
            _ => TokenKind.Identifier
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Eq(ReadOnlySpan<char> a, ReadOnlySpan<char> b) =>
        MemoryExtensions.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char Peek(int offset)
    {
        int idx = _pos + offset;
        return idx < _source.Length ? _source[idx] : '\0';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Token Make(TokenKind kind, int start, int length) =>
        new(kind, start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c == '_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentContinue(char c) =>
        char.IsLetterOrDigit(c) || c == '_';
}