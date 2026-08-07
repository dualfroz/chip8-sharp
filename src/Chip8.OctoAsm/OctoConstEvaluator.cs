using System;
using System.Collections.Generic;

namespace Chip8.OctoAsm;

public sealed class OctoConstEvaluator
{
    readonly IReadOnlyList<OctoToken> tokens;
    readonly int end;
    readonly Func<OctoToken, double> resolveName;
    int cursor;

    OctoConstEvaluator(IReadOnlyList<OctoToken> tokens, int start, int end, Func<OctoToken, double> resolveName)
    {
        this.tokens = tokens;
        this.cursor = start;
        this.end = end;
        this.resolveName = resolveName;
    }

    public static double evaluate(
        IReadOnlyList<OctoToken> tokens, int start, int end, Func<OctoToken, double> resolveName, SourcePosition where)
    {
        if (start >= end)
        {
            throw error("expected an expression", where);
        }

        var evaluator = new OctoConstEvaluator(tokens, start, end, resolveName);
        double value = evaluator.parseLogicalOr();
        if (evaluator.cursor != end)
        {
            OctoToken leftover = evaluator.tokens[evaluator.cursor];
            throw error("unexpected token " + leftover + " in expression", leftover.position);
        }
        return value;
    }

    double parseLogicalOr()
    {
        double value = parseLogicalAnd();
        while (matchWord("||"))
        {
            double right = parseLogicalAnd();
            value = truthy(value) || truthy(right) ? 1 : 0;
        }
        return value;
    }

    double parseLogicalAnd()
    {
        double value = parseEquality();
        while (matchWord("&&"))
        {
            double right = parseEquality();
            value = truthy(value) && truthy(right) ? 1 : 0;
        }
        return value;
    }

    double parseEquality()
    {
        double value = parseRelational();
        while (true)
        {
            if (matchWord("=="))
            {
                value = value == parseRelational() ? 1 : 0;
            }
            else if (matchWord("!="))
            {
                value = value != parseRelational() ? 1 : 0;
            }
            else
            {
                return value;
            }
        }
    }

    double parseRelational()
    {
        double value = parseBitOr();
        while (true)
        {
            if (matchWord("<="))
            {
                value = value <= parseBitOr() ? 1 : 0;
            }
            else if (matchWord(">="))
            {
                value = value >= parseBitOr() ? 1 : 0;
            }
            else if (matchWord("<"))
            {
                value = value < parseBitOr() ? 1 : 0;
            }
            else if (matchWord(">"))
            {
                value = value > parseBitOr() ? 1 : 0;
            }
            else
            {
                return value;
            }
        }
    }

    double parseBitOr()
    {
        double value = parseBitXor();
        while (matchWord("|"))
        {
            value = toInt(value) | toInt(parseBitXor());
        }
        return value;
    }

    double parseBitXor()
    {
        double value = parseBitAnd();
        while (matchWord("^"))
        {
            value = toInt(value) ^ toInt(parseBitAnd());
        }
        return value;
    }

    double parseBitAnd()
    {
        double value = parseShift();
        while (matchWord("&"))
        {
            value = toInt(value) & toInt(parseShift());
        }
        return value;
    }

    double parseShift()
    {
        double value = parseAdditive();
        while (true)
        {
            if (matchWord("<<"))
            {
                value = toInt(value) << (int)toInt(parseAdditive());
            }
            else if (matchWord(">>"))
            {
                value = toInt(value) >> (int)toInt(parseAdditive());
            }
            else
            {
                return value;
            }
        }
    }

    double parseAdditive()
    {
        double value = parseMultiplicative();
        while (true)
        {
            if (matchWord("+"))
            {
                value += parseMultiplicative();
            }
            else if (matchWord("-"))
            {
                value -= parseMultiplicative();
            }
            else
            {
                return value;
            }
        }
    }

    double parseMultiplicative()
    {
        double value = parseMinMax();
        while (true)
        {
            if (matchWord("*"))
            {
                value *= parseMinMax();
            }
            else if (matchWord("/"))
            {
                double divisor = parseMinMax();
                if (divisor == 0)
                {
                    throw error("division by zero", previous().position);
                }
                value /= divisor;
            }
            else if (matchWord("%"))
            {
                long divisor = toInt(parseMinMax());
                if (divisor == 0)
                {
                    throw error("modulo by zero", previous().position);
                }
                value = toInt(value) % divisor;
            }
            else
            {
                return value;
            }
        }
    }

    double parseMinMax()
    {
        double value = parsePower();
        while (true)
        {
            if (matchWord("min"))
            {
                value = Math.Min(value, parsePower());
            }
            else if (matchWord("max"))
            {
                value = Math.Max(value, parsePower());
            }
            else
            {
                return value;
            }
        }
    }

    double parsePower()
    {
        double value = parseUnary();
        if (matchWord("**") || matchWord("pow"))
        {
            return Math.Pow(value, parsePower());
        }
        return value;
    }

    double parseUnary()
    {
        if (matchWord("-"))
        {
            return -parseUnary();
        }
        if (matchWord("+"))
        {
            return parseUnary();
        }
        if (matchWord("~"))
        {
            return ~toInt(parseUnary());
        }
        if (matchWord("!"))
        {
            return truthy(parseUnary()) ? 0 : 1;
        }
        if (peekIsFunction(out string function))
        {
            advance();
            return applyFunction(function, parseUnary());
        }
        return parsePrimary();
    }

    double parsePrimary()
    {
        OctoToken token = advance();

        if (token.kind == OctoTokenKind.OpenParen)
        {
            double value = parseLogicalOr();
            expect(OctoTokenKind.CloseParen);
            return value;
        }

        if (token.kind == OctoTokenKind.Number)
        {
            return token.value;
        }

        if (token.kind == OctoTokenKind.Word)
        {
            return resolveName(token);
        }

        throw error("expected a value but found " + token, token.position);
    }

    static double applyFunction(string name, double argument) => name switch
    {
        "sqrt" => Math.Sqrt(argument),
        "sin" => Math.Sin(argument),
        "cos" => Math.Cos(argument),
        "tan" => Math.Tan(argument),
        "exp" => Math.Exp(argument),
        "log" => Math.Log(argument),
        "abs" => Math.Abs(argument),
        "sign" => Math.Sign(argument),
        "ceil" => Math.Ceiling(argument),
        "floor" => Math.Floor(argument),
        _ => throw new OctoCompileException(
            new OctoDiagnostic(OctoSeverity.Error, "unknown function '" + name + "'", new SourcePosition(0, 0))),
    };

    static readonly HashSet<string> functionNames = new()
    {
        "sqrt", "sin", "cos", "tan", "exp", "log", "abs", "sign", "ceil", "floor",
    };

    bool peekIsFunction(out string function)
    {
        if (cursor < end && tokens[cursor].kind == OctoTokenKind.Word && functionNames.Contains(tokens[cursor].text))
        {
            function = tokens[cursor].text;
            return true;
        }
        function = "";
        return false;
    }

    bool matchWord(string word)
    {
        if (cursor < end && tokens[cursor].kind == OctoTokenKind.Word && tokens[cursor].text == word)
        {
            cursor++;
            return true;
        }
        return false;
    }

    OctoToken advance()
    {
        if (cursor >= end)
        {
            throw error("unexpected end of expression", previous().position);
        }
        return tokens[cursor++];
    }

    void expect(OctoTokenKind kind)
    {
        if (cursor >= end || tokens[cursor].kind != kind)
        {
            SourcePosition where = cursor < end ? tokens[cursor].position : previous().position;
            throw error("expected " + kind, where);
        }
        cursor++;
    }

    OctoToken previous() => tokens[cursor > 0 ? cursor - 1 : 0];

    static long toInt(double value) => (long)value;

    static bool truthy(double value) => value != 0;

    static OctoCompileException error(string message, SourcePosition where) =>
        new(new OctoDiagnostic(OctoSeverity.Error, message, where));
}
