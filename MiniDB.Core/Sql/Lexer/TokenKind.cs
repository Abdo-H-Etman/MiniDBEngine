namespace MiniDB.Core.Sql.Lexer;

public enum TokenKind : byte
{
    Integer,
    Float,
    String,
    True,
    False,
    Null,

    Identifier,

    Select,
    From,
    Where,
    Insert,
    Into,
    Values,
    And,
    Or,
    Not,
    Is,
    In,
    Like,
    Between,
    Order,
    By,
    Asc,
    Desc,
    Limit,
    Offset,
    As,

    Star,
    Comma,
    Semicolon,
    LParen,
    RParen,
    Dot,

    Eq,
    NotEq,
    Lt,
    LtEq,
    Gt,
    GtEq,

    Plus,
    Minus,
    Slash,
    Percent,

    Eof
}