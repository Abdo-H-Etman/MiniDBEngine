namespace MiniDB.Core.Sql.Lexer;

public readonly struct Token
{

    public TokenKind Kind { get; }
    public int Start { get; }
    public int Length { get; }

    public Token(TokenKind kind, int start, int length)
    {
        Kind = kind;
        Start = start;
        Length = length;
    }

    public ReadOnlySpan<char> Slice(ReadOnlySpan<char> source) =>
        source.Slice(Start, Length);

    public string Text(ReadOnlySpan<char> source) =>
        new(Slice(source));

    public bool IsEof => Kind == TokenKind.Eof;
    public bool IsLiteral => Kind is TokenKind.Integer
                                  or TokenKind.Float
                                  or TokenKind.String
                                  or TokenKind.True
                                  or TokenKind.False
                                  or TokenKind.Null;

    public override string ToString() =>
        $"Token({Kind}, [{Start}..{Start + Length}])";
}