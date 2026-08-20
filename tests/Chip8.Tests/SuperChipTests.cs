using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class SuperChipTests
{
    [Fact]
    public void hiresToggleSwitchesResolution()
    {
        var machine = TestHarness.withOpcode(0x00FF, Platform.SuperChip);
        Assert.False(machine.hires);
        machine.step();
        Assert.True(machine.hires);
        Assert.Equal(Video.hiWidth, machine.video.width);
        Assert.Equal(Video.hiHeight, machine.video.height);
    }

    [Fact]
    public void loresToggleReturnsToSmallResolution()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x00FF, 0x00FE);
        machine.step();
        machine.step();
        Assert.False(machine.hires);
        Assert.Equal(Video.loWidth, machine.video.width);
    }

    [Fact]
    public void exitHaltsInterpreter()
    {
        var machine = TestHarness.withOpcode(0x00FD, Platform.SuperChip);
        machine.step();
        Assert.True(machine.exited);
        Assert.Equal(0x200, machine.pc);
    }

    [Fact]
    public void bigSpriteDrawsSixteenBySixteenBlock()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x00FF, 0xD010);
        machine.i = 0x300;
        for (int offset = 0; offset < 32; offset++)
        {
            machine.memory[0x300 + offset] = 0xFF;
        }
        machine.v[0] = 0;
        machine.v[1] = 0;

        machine.step();
        machine.step();

        Assert.Equal(256, AsciiScreen.litPixelCount(machine.video));
        Assert.Equal(0, machine.v[0xF]);
    }

    [Fact]
    public void bigSpriteReportsCollisionOnOverlap()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0xD010, 0xD010);
        machine.i = 0x300;
        for (int offset = 0; offset < 32; offset++)
        {
            machine.memory[0x300 + offset] = 0xFF;
        }

        machine.step();
        Assert.Equal(0, machine.v[0xF]);
        machine.step();
        Assert.Equal(1, machine.v[0xF]);
        Assert.Equal(0, AsciiScreen.litPixelCount(machine.video));
    }

    [Fact]
    public void rplFlagsPersistAcrossStoreAndLoad()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0xF375, 0xF385);
        machine.v[0] = 0x11;
        machine.v[1] = 0x22;
        machine.v[2] = 0x33;
        machine.v[3] = 0x44;

        machine.step();

        machine.v[0] = 0;
        machine.v[1] = 0;
        machine.v[2] = 0;
        machine.v[3] = 0;

        machine.step();

        Assert.Equal(0x11, machine.v[0]);
        Assert.Equal(0x22, machine.v[1]);
        Assert.Equal(0x33, machine.v[2]);
        Assert.Equal(0x44, machine.v[3]);
    }

    [Fact]
    public void bigFontPointsAtTenByteGlyph()
    {
        var machine = TestHarness.withOpcode(0xF030, Platform.SuperChip);
        machine.v[0] = 0x3;
        machine.step();
        Assert.Equal(0xA0 + 0x3 * FontSet.bigGlyphBytes, machine.i);
        Assert.Equal(FontSet.bigGlyphs[0x3 * FontSet.bigGlyphBytes], machine.memory[machine.i]);
    }

    [Fact]
    public void scrollDownOpcodeMovesContent()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x00C4);
        machine.memory[0x300] = 0x80;
        machine.video.drawSprite(machine.memory, 0x300, 5, 2, 1, 1, false);
        Assert.True(machine.video.pixel(0, 5, 2));

        machine.step();

        Assert.False(machine.video.pixel(0, 5, 2));
        Assert.True(machine.video.pixel(0, 5, 6));
    }

    [Fact]
    public void scrollRightOpcodeMovesContentFourPixels()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x00FB);
        machine.memory[0x300] = 0x80;
        machine.video.drawSprite(machine.memory, 0x300, 5, 2, 1, 1, false);

        machine.step();

        Assert.False(machine.video.pixel(0, 5, 2));
        Assert.True(machine.video.pixel(0, 9, 2));
    }

    [Fact]
    public void scrollingRomRendersMenuUnderSuperChip()
    {
        var machine = TestHarness.runRomExtended("8-scrolling.ch8", 1000, Platform.SuperChip);
        Assert.False(machine.exited);
        Assert.False(machine.hires);
        Assert.Equal(254, AsciiScreen.litPixelCount(machine.video));
    }
}
