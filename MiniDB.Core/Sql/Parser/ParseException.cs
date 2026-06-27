using MiniDB.Core.Sql.Lexer;

namespace MiniDB.Core.Sql.Parser;

public sealed class ParseException : Exception
{
    public Token Found { get; }
    public int Position { get; }

    public ParseException(string message, Token found, ReadOnlySpan<char> source)
        : base(Format(message, found, source))
    {
        Found = found;
        Position = found.Start;
    }
    private static string Format(string message, Token found, ReadOnlySpan<char> source)
    {
        string text = found.IsEof ? "<end of input>" : found.Text(source);
        return $"{message} (got {found.Kind} '{text}' at position {found.Start})";
    }
}