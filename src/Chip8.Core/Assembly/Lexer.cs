using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chip8.Core.Assembly;

public sealed class Lexer
{
    readonly string source;
    int offset;
    int line = 1;
    int lineStart;

    public Lexer(string source)
    {
        this.source = source;
    }

    public List<Token> tokenize()
    {
        var tokens = new List<Token>();

        while (offset < source.Length)
        {
            char c = source[offset];

            if (c == '\n')
            {
                tokens.Add(makeToken(TokenKind.EndOfLine, "\\n"));
                advanceLine();
                continue;
            }

            if (c == '\r')
            {
                offset++;
                continue;
            }

            if (c == ' ' || c == '\t')
            {
                offset++;
                continue;
            }

            if (c == ';' || (c == '/' && peek(1) == '/'))
            {
                skipToLineEnd();
                continue;
            }

            tokens.Add(scanToken());
        }

        tokens.Add(makeToken(TokenKind.EndOfLine, "\\n"));
        tokens.Add(makeToken(TokenKind.EndOfFile, ""));
        return tokens;
    }

    Token scanToken()
    {
        char c = source[offset];

        if (isIdentifierStart(c))
        {
            return scanIdentifier();
        }

        if (char.IsDigit(c) || c == '$' || c == '%')
        {
            return scanNumber();
        }

        return scanPunctuation();
    }

    Token scanIdentifier()
    {
        int column = currentColumn();
        int start = offset;
        while (offset < source.Length && isIdentifierPart(source[offset]))
        {
            offset++;
        }
        string text = source.Substring(start, offset - start);
        return new Token(TokenKind.Identifier, text, 0, new SourcePosition(line, column));
    }

    Token scanNumber()
    {
        int column = currentColumn();
        int start = offset;
        char c = source[offset];

        if (c == '$')
        {
            offset++;
            return finishRadixNumber(start, column, 16, "hex");
        }

        if (c == '%')
        {
            offset++;
            return finishRadixNumber(start, column, 2, "binary");
        }

        if (c == '0' && (peek(1) == 'x' || peek(1) == 'X'))
        {
            offset += 2;
            return finishRadixNumber(start, column, 16, "hex");
        }

        if (c == '0' && (peek(1) == 'b' || peek(1) == 'B'))
        {
            offset += 2;
            return finishRadixNumber(start, column, 2, "binary");
        }

        while (offset < source.Length && char.IsDigit(source[offset]))
        {
            offset++;
        }
        string decimalText = source.Substring(start, offset - start);
        int decimalValue = int.Parse(decimalText, CultureInfo.InvariantCulture);
        return new Token(TokenKind.Number, decimalText, decimalValue, new SourcePosition(line, column));
    }

    Token finishRadixNumber(int start, int column, int radix, string label)
    {
        int digitsStart = offset;
        while (offset < source.Length && isRadixDigit(source[offset], radix))
        {
            offset++;
        }

        if (offset == digitsStart)
        {
            throw new AsmException("malformed " + label + " literal", new SourcePosition(line, column));
        }

        string digits = source.Substring(digitsStart, offset - digitsStart);
        int value = 0;
        foreach (char digit in digits)
        {
            value = value * radix + digitValue(digit);
        }

        string text = source.Substring(start, offset - start);
        return new Token(TokenKind.Number, text, value, new SourcePosition(line, column));
    }

    Token scanPunctuation()
    {
        int column = currentColumn();
        char c = source[offset];
        char next = peek(1);

        if (c == '<' && next == '<')
        {
            offset += 2;
            return new Token(TokenKind.ShiftLeft, "<<", 0, new SourcePosition(line, column));
        }

        if (c == '>' && next == '>')
        {
            offset += 2;
            return new Token(TokenKind.ShiftRight, ">>", 0, new SourcePosition(line, column));
        }

        TokenKind kind = c switch
        {
            ',' => TokenKind.Comma,
            ':' => TokenKind.Colon,
            '-' => TokenKind.Minus,
            '+' => TokenKind.Plus,
            '*' => TokenKind.Star,
            '/' => TokenKind.Slash,
            '&' => TokenKind.Ampersand,
            '|' => TokenKind.Pipe,
            '^' => TokenKind.Caret,
            '~' => TokenKind.Tilde,
            '(' => TokenKind.LeftParen,
            ')' => TokenKind.RightParen,
            '[' => TokenKind.LeftBracket,
            ']' => TokenKind.RightBracket,
            _ => throw new AsmException("unexpected character '" + c + "'", new SourcePosition(line, column)),
        };

        offset++;
        return new Token(kind, c.ToString(), 0, new SourcePosition(line, column));
    }

    void skipToLineEnd()
    {
        while (offset < source.Length && source[offset] != '\n')
        {
            offset++;
        }
    }

    void advanceLine()
    {
        offset++;
        line++;
        lineStart = offset;
    }

    Token makeToken(TokenKind kind, string text) =>
        new(kind, text, 0, new SourcePosition(line, currentColumn()));

    int currentColumn() => offset - lineStart + 1;

    char peek(int ahead)
    {
        int index = offset + ahead;
        return index < source.Length ? source[index] : '\0';
    }

    static bool isIdentifierStart(char c) => char.IsLetter(c) || c == '_' || c == '.';

    static bool isIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';

    static bool isRadixDigit(char c, int radix) => radix switch
    {
        2 => c == '0' || c == '1',
        16 => Uri.IsHexDigit(c),
        _ => char.IsDigit(c),
    };

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
        return c - 'A' + 10;
    }
}
