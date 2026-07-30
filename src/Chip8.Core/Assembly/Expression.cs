using System;
using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public sealed class Expression
{
    readonly IReadOnlyList<Token> tokens;
    readonly Func<Token, int> resolveSymbol;
    int cursor;

    Expression(IReadOnlyList<Token> tokens, Func<Token, int> resolveSymbol)
    {
        this.tokens = tokens;
        this.resolveSymbol = resolveSymbol;
    }

    public static int evaluate(IReadOnlyList<Token> tokens, Func<Token, int> resolveSymbol, SourcePosition position)
    {
        if (tokens.Count == 0)
        {
            throw new AsmException("expected an expression", position);
        }

        var parser = new Expression(tokens, resolveSymbol);
        int value = parser.parseBitwiseOr();
        if (parser.cursor != tokens.Count)
        {
            throw new AsmException("unexpected token '" + tokens[parser.cursor].text + "' in expression", tokens[parser.cursor].position);
        }
        return value;
    }

    int parseBitwiseOr()
    {
        int value = parseBitwiseXor();
        while (match(TokenKind.Pipe))
        {
            value |= parseBitwiseXor();
        }
        return value;
    }

    int parseBitwiseXor()
    {
        int value = parseBitwiseAnd();
        while (match(TokenKind.Caret))
        {
            value ^= parseBitwiseAnd();
        }
        return value;
    }

    int parseBitwiseAnd()
    {
        int value = parseShift();
        while (match(TokenKind.Ampersand))
        {
            value &= parseShift();
        }
        return value;
    }

    int parseShift()
    {
        int value = parseAdditive();
        while (true)
        {
            if (match(TokenKind.ShiftLeft))
            {
                value <<= parseAdditive();
            }
            else if (match(TokenKind.ShiftRight))
            {
                value >>= parseAdditive();
            }
            else
            {
                return value;
            }
        }
    }

    int parseAdditive()
    {
        int value = parseMultiplicative();
        while (true)
        {
            if (match(TokenKind.Plus))
            {
                value += parseMultiplicative();
            }
            else if (match(TokenKind.Minus))
            {
                value -= parseMultiplicative();
            }
            else
            {
                return value;
            }
        }
    }

    int parseMultiplicative()
    {
        int value = parseUnary();
        while (true)
        {
            if (match(TokenKind.Star))
            {
                value *= parseUnary();
            }
            else if (match(TokenKind.Slash))
            {
                int divisor = parseUnary();
                if (divisor == 0)
                {
                    throw new AsmException("division by zero", previous().position);
                }
                value /= divisor;
            }
            else if (match(TokenKind.Percent))
            {
                int divisor = parseUnary();
                if (divisor == 0)
                {
                    throw new AsmException("modulo by zero", previous().position);
                }
                value %= divisor;
            }
            else
            {
                return value;
            }
        }
    }

    int parseUnary()
    {
        if (match(TokenKind.Minus))
        {
            return -parseUnary();
        }
        if (match(TokenKind.Plus))
        {
            return parseUnary();
        }
        if (match(TokenKind.Tilde))
        {
            return ~parseUnary();
        }
        return parsePrimary();
    }

    int parsePrimary()
    {
        Token token = advance();

        if (token.kind == TokenKind.LeftParen)
        {
            int value = parseBitwiseOr();
            expect(TokenKind.RightParen);
            return value;
        }

        if (token.kind == TokenKind.Number)
        {
            return token.value;
        }

        if (token.kind == TokenKind.Identifier)
        {
            return resolveSymbol(token);
        }

        throw new AsmException("expected a value but found '" + token.text + "'", token.position);
    }

    bool match(TokenKind kind)
    {
        if (cursor < tokens.Count && tokens[cursor].kind == kind)
        {
            cursor++;
            return true;
        }
        return false;
    }

    Token advance()
    {
        if (cursor >= tokens.Count)
        {
            throw new AsmException("unexpected end of expression", tokens[tokens.Count - 1].position);
        }
        return tokens[cursor++];
    }

    void expect(TokenKind kind)
    {
        if (!match(kind))
        {
            SourcePosition position = cursor < tokens.Count ? tokens[cursor].position : tokens[tokens.Count - 1].position;
            throw new AsmException("expected '" + kind + "'", position);
        }
    }

    Token previous() => tokens[cursor - 1];
}
