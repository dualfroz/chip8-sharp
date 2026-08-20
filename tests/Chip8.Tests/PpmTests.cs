using System.Text;
using Chip8.Core;
using Chip8.Core.Export;
using Xunit;

namespace Chip8.Tests;

public class PpmTests
{
    static int headerLength(byte[] ppm)
    {
        int newlines = 0;
        for (int index = 0; index < ppm.Length; index++)
        {
            if (ppm[index] == (byte)'\n' && ++newlines == 3)
            {
                return index + 1;
            }
        }
        return -1;
    }

    [Fact]
    public void monochromeImageHasCorrectHeaderAndSize()
    {
        var pixels = new bool[2 * 2];
        byte[] ppm = PpmImage.fromMonochrome(pixels, 2, 2, Palette.monochrome());

        string header = Encoding.ASCII.GetString(ppm, 0, headerLength(ppm));
        Assert.Equal("P6\n2 2\n255\n", header);
        Assert.Equal(headerLength(ppm) + 2 * 2 * 3, ppm.Length);
    }

    [Fact]
    public void litAndUnlitPixelsUsePaletteColors()
    {
        var pixels = new bool[] { true, false, false, true };
        var palette = new Palette(new Rgb(1, 2, 3), new Rgb(10, 20, 30));
        byte[] ppm = PpmImage.fromMonochrome(pixels, 2, 2, palette);
        int start = headerLength(ppm);

        Assert.Equal(10, ppm[start]);
        Assert.Equal(20, ppm[start + 1]);
        Assert.Equal(30, ppm[start + 2]);
        Assert.Equal(1, ppm[start + 3]);
        Assert.Equal(2, ppm[start + 4]);
        Assert.Equal(3, ppm[start + 5]);
    }

    [Fact]
    public void scaleMultipliesDimensions()
    {
        var pixels = new bool[1];
        byte[] ppm = PpmImage.fromMonochrome(pixels, 1, 1, Palette.monochrome(), scale: 4);
        string header = Encoding.ASCII.GetString(ppm, 0, headerLength(ppm));
        Assert.Equal("P6\n4 4\n255\n", header);
        Assert.Equal(headerLength(ppm) + 4 * 4 * 3, ppm.Length);
    }

    [Fact]
    public void videoExportUsesColorPlanes()
    {
        var video = new Video();
        video.setResolution(true);
        var sprite = new byte[] { 0x80 };
        video.drawSprite(sprite, 0, 0, 0, 1, 0x2, false);

        byte[] ppm = PpmImage.fromVideo(video, Palette.octo());
        int start = headerLength(ppm);
        Rgb colorTwo = Palette.octo()[2];

        Assert.Equal(colorTwo.r, ppm[start]);
        Assert.Equal(colorTwo.g, ppm[start + 1]);
        Assert.Equal(colorTwo.b, ppm[start + 2]);
    }

    [Fact]
    public void fromMachinePicksMonochromeForChip8()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.display[0] = true;
        byte[] ppm = PpmImage.fromMachine(machine, Palette.monochrome());
        int start = headerLength(ppm);
        Assert.Equal(0xFF, ppm[start]);
    }
}
