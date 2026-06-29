namespace Chip8.Core.Assembly;

public enum TokenKind
{
    Identifier,
    Number,
    Comma,
    Colon,
    Minus,
    Plus,
    Star,
    Slash,
    Percent,
    Ampersand,
    Pipe,
    Caret,
    Tilde,
    ShiftLeft,
    ShiftRight,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    EndOfLine,
    EndOfFile,
}

public readonly struct SourcePosition
{
    public SourcePosition(int line, int column)
    {
        this.line = line;
        this.column = column;
    }

    public int line { get; }
    public int column { get; }

    public override string ToString() => "line " + line + ", col " + column;
}

public readonly struct Token
{
    public Token(TokenKind kind, string text, int value, SourcePosition position)
    {
        this.kind = kind;
        this.text = text;
        this.value = value;
        this.position = position;
    }

    public TokenKind kind { get; }
    public string text { get; }
    public int value { get; }
    public SourcePosition position { get; }

    public bool isIdentifier(string name) =>
        kind == TokenKind.Identifier && string.Equals(text, name, System.StringComparison.OrdinalIgnoreCase);

    public override string ToString() => kind + "('" + text + "')";
}
