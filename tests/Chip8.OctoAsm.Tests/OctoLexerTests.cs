using System.Collections.Generic;
using Chip8.OctoAsm;
using Xunit;

namespace Chip8.OctoAsm.Tests;

public class OctoLexerTests
{
    [Fact]
    public void splitsWordsNumbersAndBracketsWithPositions()
    {
        List<OctoToken> tokens = OctoLexer.tokenize(": main\n  v0 := 0x1F\n:calc x { ( 1 + 2 ) }");

        Assert.Equal(OctoTokenKind.Word, tokens[0].kind);
        Assert.Equal(":", tokens[0].text);
        Assert.Equal("main", tokens[1].text);
        Assert.Equal(new[] { 2, 3 }, new[] { tokens[2].position.line, tokens[2].position.column });
        Assert.Equal(":=", tokens[3].text);
        Assert.Equal(OctoTokenKind.Number, tokens[4].kind);
        Assert.Equal(0x1F, tokens[4].value);
        Assert.Equal(OctoTokenKind.OpenBrace, tokens[7].kind);
        Assert.Equal(OctoTokenKind.OpenParen, tokens[8].kind);
        Assert.Equal(OctoTokenKind.CloseParen, tokens[12].kind);
        Assert.Equal(OctoTokenKind.CloseBrace, tokens[13].kind);
        Assert.Equal(OctoTokenKind.EndOfFile, tokens[tokens.Count - 1].kind);
    }

    [Fact]
    public void skipsCommentsToEndOfLine()
    {
        List<OctoToken> tokens = OctoLexer.tokenize("clear # wipe the screen\n# whole line\nreturn");

        Assert.Equal(3, tokens.Count);
        Assert.True(tokens[0].isWord("clear"));
        Assert.True(tokens[1].isWord("return"));
        Assert.Equal(3, tokens[1].position.line);
    }

    [Fact]
    public void bracketsDelimitWordsWithoutSpaces()
    {
        List<OctoToken> tokens = OctoLexer.tokenize("{x}");

        Assert.Equal(OctoTokenKind.OpenBrace, tokens[0].kind);
        Assert.True(tokens[1].isWord("x"));
        Assert.Equal(OctoTokenKind.CloseBrace, tokens[2].kind);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("0x2A", 42)]
    [InlineData("0X2a", 42)]
    [InlineData("0b101010", 42)]
    [InlineData("-42", -42)]
    [InlineData("-0x10", -16)]
    public void parsesNumberLiterals(string text, int expected)
    {
        Assert.True(OctoLexer.tryParseNumber(text, out int value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("0x")]
    [InlineData("0b102")]
    [InlineData("12ab")]
    [InlineData("v0")]
    public void rejectsNonNumbers(string text)
    {
        Assert.False(OctoLexer.tryParseNumber(text, out _));
    }

    [Fact]
    public void emptySourceYieldsOnlyEndOfFile()
    {
        List<OctoToken> tokens = OctoLexer.tokenize("   \n\t ");

        Assert.Single(tokens);
        Assert.Equal("<eof>", tokens[0].ToString());
    }
}
