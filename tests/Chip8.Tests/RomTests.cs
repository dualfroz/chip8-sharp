using System.Text;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class RomTests
{
    const int selfCheckCount = 17;

    static readonly string[] seventeenGlyphs =
    {
        "  #  ####",
        " ##     #",
        "  #    #",
        "  #   #",
        " ###  #",
    };

    [Theory]
    [InlineData("1-chip8-logo.ch8", 400, "logo.txt", 520)]
    [InlineData("2-ibm-logo.ch8", 40, "ibm.txt", 230)]
    [InlineData("3-corax+.ch8", 500, "corax.txt", 503)]
    [InlineData("4-flags.ch8", 1000, "flags.txt", 495)]
    [InlineData("test_opcode.ch8", 3000, "opcode.txt", 626)]
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

    public static TheoryData<string> quirkProfiles() => new() { "modern", "cosmac" };

    [Theory]
    [MemberData(nameof(quirkProfiles))]
    public void assembledSelfCheckPassesEveryCheck(string profile)
    {
        Quirks quirks = profile == "cosmac" ? Quirks.cosmacVip() : Quirks.modern();
        var machine = TestHarness.runProgram(TestHarness.assembledRom("selfcheck.asm"), 4000, quirks);

        Assert.Equal(0, machine.v[0xD]);
        Assert.Equal(selfCheckCount, machine.v[0xE]);
        Assert.Equal(expectedSelfCheckScreen(), AsciiScreen.render(machine.display));
    }

    static string expectedSelfCheckScreen()
    {
        var builder = new StringBuilder();
        for (int row = 0; row < Machine.displayHeight; row++)
        {
            string line = row < seventeenGlyphs.Length ? seventeenGlyphs[row] : "";
            builder.Append(line.PadRight(Machine.displayWidth)).Append('\n');
        }
        return builder.ToString();
    }
}
