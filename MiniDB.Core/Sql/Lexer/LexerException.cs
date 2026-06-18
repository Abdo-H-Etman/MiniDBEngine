namespace MiniDB.Core.Sql.Lexer;

public sealed class LexerException : Exception
{
    public int Position { get; }
    public char UnexpectedChar { get; }

    public LexerException(char ch, int position)
        : base($"Unexpected character '{ch}' at position {position}.")
    {
        Position = position;
        UnexpectedChar = ch;
    }

    public LexerException(string message, int position)
        : base($"{message} (at position {position}).")
    {
        Position = position;
        UnexpectedChar = '\0';
    }
}