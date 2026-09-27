using System;
using System.Collections.Generic;
using Chip8.OctoAsm;
using Xunit;

namespace Chip8.OctoAsm.Tests;

public class OctoConstEvaluatorTests
{
    static double evaluate(string expression, Func<OctoToken, double>? resolve = null)
    {
        List<OctoToken> tokens = OctoLexer.tokenize(expression);
        return OctoConstEvaluator.evaluate(
            tokens,
            0,
            tokens.Count - 1,
            resolve ?? (token => throw new InvalidOperationException("unexpected name " + token.text)),
            new SourcePosition(1, 1));
    }

    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("( 2 + 3 ) * 4", 20)]
    [InlineData("10 - 4 - 3", 3)]
    [InlineData("7 / 2", 3.5)]
    [InlineData("7 % 4", 3)]
    [InlineData("1 << 4 | 1", 17)]
    [InlineData("0xF0 >> 4", 15)]
    [InlineData("0xFF & 0x0F ^ 0x03", 12)]
    [InlineData("2 ** 3 ** 2", 512)]
    [InlineData("2 pow 10", 1024)]
    [InlineData("3 min 8", 3)]
    [InlineData("3 max 8", 8)]
    [InlineData("- 5 + 2", -3)]
    [InlineData("~ 0 & 0xFF", 255)]
    [InlineData("! 0", 1)]
    [InlineData("! 7", 0)]
    [InlineData("3 < 4", 1)]
    [InlineData("3 >= 4", 0)]
    [InlineData("2 == 2 && 3 != 3", 0)]
    [InlineData("2 == 2 || 3 != 3", 1)]
    [InlineData("sqrt 16", 4)]
    [InlineData("floor ( 7 / 2 )", 3)]
    [InlineData("abs - 9", 9)]
    public void evaluatesWithOctoPrecedence(string expression, double expected)
    {
        Assert.Equal(expected, evaluate(expression), 9);
    }

    [Fact]
    public void namesGoThroughTheResolver()
    {
        double value = evaluate("base + width * 2", token => token.text == "base" ? 0x200 : 8);
        Assert.Equal(0x210, value);
    }

    [Theory]
    [InlineData("1 / 0", "division by zero")]
    [InlineData("5 % 0", "modulo by zero")]
    [InlineData("1 2", "unexpected token")]
    [InlineData("( 1 + 2", "expected CloseParen")]
    [InlineData("1 +", "unexpected end of expression")]
    public void reportsMalformedExpressions(string expression, string message)
    {
        var error = Assert.Throws<OctoCompileException>(() => evaluate(expression));
        Assert.Contains(message, error.Message);
        Assert.Equal(OctoSeverity.Error, error.diagnostic.severity);
    }

    [Fact]
    public void emptyRangeIsAnError()
    {
        List<OctoToken> tokens = OctoLexer.tokenize("");
        var error = Assert.Throws<OctoCompileException>(() =>
            OctoConstEvaluator.evaluate(tokens, 0, 0, _ => 0, new SourcePosition(4, 2)));
        Assert.Equal(4, error.diagnostic.line);
        Assert.Equal(2, error.diagnostic.column);
    }
}
