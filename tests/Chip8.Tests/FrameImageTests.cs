using Chip8.Core;
using Chip8.Core.Export;
using Xunit;

namespace Chip8.Tests;

public class FrameImageTests
{
    static void runSteps(Machine machine, int steps)
    {
        for (int step = 0; step < steps; step++)
        {
            machine.step();
        }
    }

    [Fact]
    public void chip8FrameIsLowResolutionInPaletteColors()
    {
        var palette = new Palette(new Rgb(1, 2, 3), new Rgb(200, 100, 50));
        var machine = new Machine(Quirks.modern(), 1);
        machine.display[5 * Machine.displayWidth + 7] = true;

        FrameImage image = FrameImage.fromMachine(machine, palette);

        Assert.Equal(64, image.width);
        Assert.Equal(32, image.height);
        Assert.Equal(64 * 32 * FrameImage.bytesPerPixel, image.rgba.Length);
        Assert.Equal(palette[1], image.colorAt(7, 5));
        Assert.Equal(palette[0], image.colorAt(8, 5));
        Assert.Equal(0xFF, image.rgba[3]);
    }

    [Fact]
    public void superChipFrameFollowsResolutionSwitches()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip,
            0x00FF, 0xA300, 0x6064, 0x6128, 0xD011, 0x00FE, 0x6014, 0x610A, 0xD011);
        machine.memory[0x300] = 0x80;
        Palette palette = Palette.octo();

        runSteps(machine, 5);
        FrameImage hires = FrameImage.fromMachine(machine, palette);
        runSteps(machine, 4);
        FrameImage lores = FrameImage.fromMachine(machine, palette);

        Assert.Equal((128, 64), (hires.width, hires.height));
        Assert.Equal(palette[1], hires.colorAt(100, 40));
        Assert.Equal(palette[0], hires.colorAt(99, 40));
        Assert.Equal((64, 32), (lores.width, lores.height));
        Assert.Equal(palette[1], lores.colorAt(20, 10));
        Assert.Equal(64 * 32 * FrameImage.bytesPerPixel, lores.rgba.Length);
    }

    [Fact]
    public void xoChipFrameShowsEveryPlaneCombination()
    {
        var machine = TestHarness.withProgram(Platform.XoChip,
            0x00FF, 0xA300, 0x6103, 0x6005, 0xF101, 0xD011, 0x600A, 0xF201, 0xD011, 0x600F, 0xF301, 0xD011);
        machine.memory[0x300] = 0x80;
        machine.memory[0x301] = 0x80;
        Palette palette = Palette.octo();

        runSteps(machine, 12);
        FrameImage image = FrameImage.fromMachine(machine, palette);

        Assert.Equal((128, 64), (image.width, image.height));
        Assert.Equal(palette[0], image.colorAt(0, 0));
        Assert.Equal(palette[1], image.colorAt(5, 3));
        Assert.Equal(palette[2], image.colorAt(10, 3));
        Assert.Equal(palette[3], image.colorAt(15, 3));
    }

    [Fact]
    public void ppmExportUsesTheSameFrame()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x00FF, 0xA300, 0xF301, 0xD012);
        machine.memory[0x300] = 0xF0;
        machine.memory[0x301] = 0x0F;
        machine.memory[0x302] = 0x3C;
        machine.memory[0x303] = 0xC3;
        runSteps(machine, 4);

        FrameImage image = FrameImage.fromMachine(machine, Palette.extended16());
        byte[] ppm = PpmImage.fromMachine(machine, Palette.extended16());

        byte[] header = System.Text.Encoding.ASCII.GetBytes("P6\n128 64\n255\n");
        Assert.Equal(header, ppm[..header.Length]);
        for (int pixel = 0; pixel < image.width * image.height; pixel++)
        {
            int ppmOffset = header.Length + pixel * 3;
            int rgbaOffset = pixel * FrameImage.bytesPerPixel;
            Assert.Equal(image.rgba[rgbaOffset..(rgbaOffset + 3)], ppm[ppmOffset..(ppmOffset + 3)]);
        }
    }

    [Theory]
    [InlineData(64, 32, 896, 448, 0, 0, 14)]
    [InlineData(128, 64, 896, 448, 0, 0, 7)]
    [InlineData(128, 64, 1000, 448, 52, 0, 7)]
    [InlineData(64, 32, 640, 600, 0, 140, 10)]
    [InlineData(128, 64, 100, 40, -14, -12, 1)]
    public void layoutScalesByWholePixelsAndCenters(int frameWidth, int frameHeight, int viewWidth, int viewHeight, int x, int y, int pixelSize)
    {
        FrameLayout layout = FrameLayout.fit(frameWidth, frameHeight, viewWidth, viewHeight);

        Assert.Equal((x, y, pixelSize), (layout.x, layout.y, layout.pixelSize));
        Assert.Equal((frameWidth * pixelSize, frameHeight * pixelSize), (layout.width, layout.height));
    }

    [Theory]
    [InlineData(Platform.Chip8, 14, 896, 448)]
    [InlineData(Platform.Chip8, 5, 320, 160)]
    [InlineData(Platform.SuperChip, 14, 896, 448)]
    [InlineData(Platform.XoChip, 5, 384, 192)]
    [InlineData(Platform.XoChip, 1, 128, 64)]
    public void windowFitsBothResolutionsWithWholePixels(Platform platform, int scale, int width, int height)
    {
        Assert.Equal((width, height), FrameLayout.windowSize(platform, scale));

        FrameLayout lores = FrameLayout.fit(Video.loWidth, Video.loHeight, width, height);
        Assert.Equal((width, height), (lores.width, lores.height));
        if (platform != Platform.Chip8)
        {
            FrameLayout hires = FrameLayout.fit(Video.hiWidth, Video.hiHeight, width, height);
            Assert.Equal((width, height), (hires.width, hires.height));
            Assert.Equal(lores.pixelSize, hires.pixelSize * 2);
        }
    }
}
