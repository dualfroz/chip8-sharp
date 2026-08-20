using System;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class PaletteTests
{
    [Fact]
    public void hexParsesWithAndWithoutHash()
    {
        var withHash = Rgb.fromHex("#1A2B3C");
        var without = Rgb.fromHex("1A2B3C");
        Assert.Equal(withHash, without);
        Assert.Equal(0x1A, withHash.r);
        Assert.Equal(0x2B, withHash.g);
        Assert.Equal(0x3C, withHash.b);
    }

    [Fact]
    public void hexRoundTripsThroughToHex()
    {
        var color = new Rgb(0xFF, 0x80, 0x00);
        Assert.Equal("#FF8000", color.toHex());
        Assert.Equal(color, Rgb.fromHex(color.toHex()));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("#12")]
    [InlineData("XYZXYZ")]
    public void malformedHexThrows(string value)
    {
        Assert.Throws<FormatException>(() => Rgb.fromHex(value));
    }

    [Fact]
    public void monochromePaletteHasTwoColors()
    {
        var palette = Palette.monochrome();
        Assert.Equal(2, palette.colorCount);
        Assert.Equal(new Rgb(0, 0, 0), palette[0]);
        Assert.Equal(new Rgb(0xFF, 0xFF, 0xFF), palette[1]);
    }

    [Fact]
    public void octoPaletteHasFourColors()
    {
        var palette = Palette.octo();
        Assert.Equal(4, palette.colorCount);
        Assert.Equal(Rgb.fromHex("#996600"), palette[0]);
        Assert.Equal(Rgb.fromHex("#662200"), palette[3]);
    }

    [Fact]
    public void extendedPaletteHasSixteenColors()
    {
        Assert.Equal(16, Palette.extended16().colorCount);
    }

    [Fact]
    public void resolveWrapsIndexIntoRange()
    {
        var palette = Palette.grayscale();
        Assert.Equal(palette[0], palette.resolve(4));
        Assert.Equal(palette[1], palette.resolve(5));
        Assert.Equal(palette[0], palette.resolve(-1));
    }

    [Theory]
    [InlineData("mono")]
    [InlineData("grayscale")]
    [InlineData("octo")]
    [InlineData("extended16")]
    public void byNameResolvesKnownPalettes(string name)
    {
        Assert.True(Palette.byName(name).colorCount >= 2);
    }

    [Fact]
    public void byNameRejectsUnknownPalette()
    {
        Assert.Throws<ArgumentException>(() => Palette.byName("neon"));
    }

    [Fact]
    public void paletteRejectsSingleColor()
    {
        Assert.Throws<ArgumentException>(() => new Palette(new Rgb(0, 0, 0)));
    }
}
