using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class QuirksTests
{
    static Machine drawRowAt(int originX, Quirks quirks)
    {
        var machine = new Machine(quirks, 1);
        machine.memory[0x200] = 0xD0;
        machine.memory[0x201] = 0x11;
        machine.i = 0x300;
        machine.memory[0x300] = 0xFF;
        machine.v[0] = (byte)originX;
        machine.v[1] = 0;
        machine.step();
        return machine;
    }

    [Fact]
    public void clipQuirkTruncatesSpriteAtRightEdge()
    {
        var clipping = new Quirks { clipSprites = true };
        var machine = drawRowAt(62, clipping);
        Assert.Equal(2, AsciiScreen.litPixelCount(machine.display));
        Assert.True(machine.display[62]);
        Assert.True(machine.display[63]);
        Assert.False(machine.display[0]);
    }

    [Fact]
    public void wrapQuirkWrapsSpriteAroundRightEdge()
    {
        var wrapping = new Quirks { clipSprites = false };
        var machine = drawRowAt(62, wrapping);
        Assert.Equal(8, AsciiScreen.litPixelCount(machine.display));
        Assert.True(machine.display[62]);
        Assert.True(machine.display[63]);
        Assert.True(machine.display[0]);
        Assert.True(machine.display[5]);
        Assert.False(machine.display[6]);
    }

    [Fact]
    public void shiftQuirkSelectsSourceRegister()
    {
        var usesVx = TestHarness.withOpcode(0x8016, new Quirks { shiftUsesVy = false });
        usesVx.v[0] = 0b0000_0100;
        usesVx.v[1] = 0xFF;
        usesVx.step();
        Assert.Equal(0b0000_0010, usesVx.v[0]);

        var usesVy = TestHarness.withOpcode(0x8016, new Quirks { shiftUsesVy = true });
        usesVy.v[0] = 0x00;
        usesVy.v[1] = 0b0000_0100;
        usesVy.step();
        Assert.Equal(0b0000_0010, usesVy.v[0]);
    }

    [Fact]
    public void loadStoreIncrementQuirkAdvancesIndex()
    {
        var noIncrement = TestHarness.withOpcode(0xF255, new Quirks { loadStoreIncrementsI = false });
        noIncrement.i = 0x300;
        noIncrement.step();
        Assert.Equal(0x300, noIncrement.i);

        var increment = TestHarness.withOpcode(0xF255, new Quirks { loadStoreIncrementsI = true });
        increment.i = 0x300;
        increment.step();
        Assert.Equal(0x303, increment.i);
    }

    [Fact]
    public void jumpQuirkSelectsOffsetRegister()
    {
        var withV0 = TestHarness.withOpcode(0xB300, new Quirks { jumpWithVx = false });
        withV0.v[0] = 0x10;
        withV0.v[3] = 0x20;
        withV0.step();
        Assert.Equal(0x310, withV0.pc);

        var withVx = TestHarness.withOpcode(0xB300, new Quirks { jumpWithVx = true });
        withVx.v[0] = 0x10;
        withVx.v[3] = 0x20;
        withVx.step();
        Assert.Equal(0x320, withVx.pc);
    }

    [Fact]
    public void vfResetQuirkClearsFlagOnLogic()
    {
        var noReset = TestHarness.withOpcode(0x8011, new Quirks { resetVfOnLogic = false });
        noReset.v[0xF] = 0x5;
        noReset.step();
        Assert.Equal(0x5, noReset.v[0xF]);

        var reset = TestHarness.withOpcode(0x8011, new Quirks { resetVfOnLogic = true });
        reset.v[0xF] = 0x5;
        reset.step();
        Assert.Equal(0, reset.v[0xF]);
    }
}
