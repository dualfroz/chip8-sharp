using System.Collections.Generic;

namespace Chip8.OctoAsm;

public sealed class OctoLexer
{
    readonly string source;
    int offset;
    int line = 1;
    int lineStart;

    public OctoLexer(string source)
    {
        this.source = source;
    }

    public static List<OctoToken> tokenize(string source) => new OctoLexer(source).scan();

    public List<OctoToken> scan()
    {
        var tokens = new List<OctoToken>();

        while (offset < source.Length)
        {
            char c = source[offset];

            if (c == '\n')
            {
                offset++;
                line++;
                lineStart = offset;
                continue;
            }

            if (c == '\r' || c == ' ' || c == '\t')
            {
                offset++;
                continue;
            }

            if (c == '#')
            {
                skipComment();
                continue;
            }

            if (isBracket(c))
            {
                tokens.Add(makeBracket(c));
                offset++;
                continue;
            }

            tokens.Add(scanWord());
        }

        tokens.Add(new OctoToken(OctoTokenKind.EndOfFile, "", 0, position()));
        return tokens;
    }

    OctoToken scanWord()
    {
        SourcePosition start = position();
        int begin = offset;

        while (offset < source.Length && !isDelimiter(source[offset]))
        {
            offset++;
        }

        string text = source.Substring(begin, offset - begin);
        if (tryParseNumber(text, out int value))
        {
            return new OctoToken(OctoTokenKind.Number, text, value, start);
        }

        return new OctoToken(OctoTokenKind.Word, text, 0, start);
    }

    OctoToken makeBracket(char c)
    {
        OctoTokenKind kind = c switch
        {
            '{' => OctoTokenKind.OpenBrace,
            '}' => OctoTokenKind.CloseBrace,
            '(' => OctoTokenKind.OpenParen,
            _ => OctoTokenKind.CloseParen,
        };
        return new OctoToken(kind, c.ToString(), 0, position());
    }

    void skipComment()
    {
        while (offset < source.Length && source[offset] != '\n')
        {
            offset++;
        }
    }

    SourcePosition position() => new(line, offset - lineStart + 1);

    static bool isBracket(char c) => c == '{' || c == '}' || c == '(' || c == ')';

    static bool isDelimiter(char c) =>
        c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '#' || isBracket(c);

    public static bool tryParseNumber(string text, out int value)
    {
        value = 0;
        if (text.Length == 0)
        {
            return false;
        }

        int index = 0;
        bool negative = false;
        if (text[0] == '-')
        {
            negative = true;
            index = 1;
        }

        if (index >= text.Length)
        {
            return false;
        }

        bool ok;
        long magnitude;
        if (hasPrefix(text, index, "0x") || hasPrefix(text, index, "0X"))
        {
            ok = parseRadix(text, index + 2, 16, out magnitude);
        }
        else if (hasPrefix(text, index, "0b") || hasPrefix(text, index, "0B"))
        {
            ok = parseRadix(text, index + 2, 2, out magnitude);
        }
        else
        {
            ok = parseRadix(text, index, 10, out magnitude);
        }

        if (!ok)
        {
            return false;
        }

        long signed = negative ? -magnitude : magnitude;
        value = (int)signed;
        return true;
    }

    static bool hasPrefix(string text, int index, string prefix) =>
        index + prefix.Length <= text.Length && text.Substring(index, prefix.Length) == prefix;

    static bool parseRadix(string text, int start, int radix, out long value)
    {
        value = 0;
        if (start >= text.Length)
        {
            return false;
        }

        for (int i = start; i < text.Length; i++)
        {
            int digit = digitValue(text[i]);
            if (digit < 0 || digit >= radix)
            {
                return false;
            }
            value = value * radix + digit;
        }
        return true;
    }

    static int digitValue(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }
        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }
        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }
        return -1;
    }
}
