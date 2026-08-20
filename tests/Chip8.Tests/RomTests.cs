using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class RomTests
{
    [Theory]
    [InlineData("2-ibm-logo.ch8", 40, "ibm.txt", 230)]
    [InlineData("3-corax+.ch8", 500, "corax.txt", 503)]
    [InlineData("4-flags.ch8", 1000, "flags.txt", 495)]
    [InlineData("test_opcode.ch8", 3000, "opcode.txt", 626)]
    [InlineData("BC_test.ch8", 4000, "bctest.txt", 172)]
    public void romRendersExpectedFramebuffer(string rom, int cycles, string golden, int litPixels)
    {
        var machine = TestHarness.runRom(rom, cycles);
        string rendered = AsciiScreen.render(machine.display);

        Assert.Equal(litPixels, AsciiScreen.litPixelCount(machine.display));
        Assert.Equal(TestHarness.goldenText(golden), rendered);
    }

    [Fact]
    public void corax89PassesUnderCosmacQuirksToo()
    {
        var machine = TestHarness.runRom("test_opcode.ch8", 3000, Quirks.cosmacVip());
        Assert.Equal(TestHarness.goldenText("opcode.txt"), AsciiScreen.render(machine.display));
    }

    [Fact]
    public void beepRomActivatesSoundTimer()
    {
        var machine = TestHarness.runRom("7-beep.ch8", 40);
        Assert.True(machine.soundActive);
    }
}
