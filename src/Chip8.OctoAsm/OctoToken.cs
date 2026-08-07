namespace Chip8.OctoAsm;

public enum OctoTokenKind
{
    Word,
    Number,
    OpenBrace,
    CloseBrace,
    OpenParen,
    CloseParen,
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

public readonly struct OctoToken
{
    public OctoToken(OctoTokenKind kind, string text, int value, SourcePosition position)
    {
        this.kind = kind;
        this.text = text;
        this.value = value;
        this.position = position;
    }

    public OctoTokenKind kind { get; }
    public string text { get; }
    public int value { get; }
    public SourcePosition position { get; }

    public bool isWord(string word) => kind == OctoTokenKind.Word && text == word;

    public override string ToString() => kind switch
    {
        OctoTokenKind.EndOfFile => "<eof>",
        OctoTokenKind.Number => "number(" + value + ")",
        _ => "'" + text + "'",
    };
}
