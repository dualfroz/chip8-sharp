using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class OpcodeTests
{
    [Fact]
    public void clearScreenBlanksDisplay()
    {
        var machine = TestHarness.withOpcode(0x00E0);
        machine.display[100] = true;
        machine.step();
        Assert.All(machine.display, pixel => Assert.False(pixel));
    }

    [Fact]
    public void jumpSetsProgramCounter()
    {
        var machine = TestHarness.withOpcode(0x1ABC);
        machine.step();
        Assert.Equal(0xABC, machine.pc);
    }

    [Fact]
    public void callPushesReturnAddressAndReturnRestoresIt()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.memory[0x200] = 0x22; machine.memory[0x201] = 0x06; // 2206 call 0x206
        machine.memory[0x206] = 0x00; machine.memory[0x207] = 0xEE; // 00EE ret
        machine.step();
        Assert.Equal(0x206, machine.pc);
        Assert.Equal(1, machine.sp);
        Assert.Equal(0x202, machine.stack[0]);
        machine.step();
        Assert.Equal(0x202, machine.pc);
        Assert.Equal(0, machine.sp);
    }

    [Fact]
    public void skipIfEqualImmediate()
    {
        var taken = TestHarness.withOpcode(0x3042);
        taken.v[0] = 0x42; taken.step();
        Assert.Equal(0x204, taken.pc);

        var skipped = TestHarness.withOpcode(0x3042);
        skipped.v[0] = 0x00; skipped.step();
        Assert.Equal(0x202, skipped.pc);
    }

    [Fact]
    public void skipIfNotEqualImmediate()
    {
        var taken = TestHarness.withOpcode(0x4042);
        taken.v[0] = 0x00; taken.step();
        Assert.Equal(0x204, taken.pc);
    }

    [Fact]
    public void skipIfRegistersEqualAndNotEqual()
    {
        var equal = TestHarness.withOpcode(0x5010);
        equal.v[0] = 9; equal.v[1] = 9; equal.step();
        Assert.Equal(0x204, equal.pc);

        var notEqual = TestHarness.withOpcode(0x9010);
        notEqual.v[0] = 9; notEqual.v[1] = 8; notEqual.step();
        Assert.Equal(0x204, notEqual.pc);
    }

    [Fact]
    public void loadImmediateAndSetIndex()
    {
        var load = TestHarness.withOpcode(0x6A55);
        load.step();
        Assert.Equal(0x55, load.v[0xA]);

        var index = TestHarness.withOpcode(0xA123);
        index.step();
        Assert.Equal(0x123, index.i);
    }

    [Fact]
    public void jumpWithOffsetHonoursQuirk()
    {
        var classic = TestHarness.withOpcode(0xB300, Quirks.modern());
        classic.v[0] = 0x12; classic.step();
        Assert.Equal(0x312, classic.pc);

        var superchip = TestHarness.withOpcode(0xB300, Quirks.superChip());
        superchip.v[3] = 0x12; superchip.step();
        Assert.Equal(0x312, superchip.pc);
    }

    [Fact]
    public void randomMaskedByImmediate()
    {
        var machine = TestHarness.withOpcode(0xC000);
        machine.step();
        Assert.Equal(0, machine.v[0]);
    }

    [Fact]
    public void drawSetsPixelsAndReportsCollision()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.memory[0x200] = 0xD0; machine.memory[0x201] = 0x11; // DXYN draw 1 row at (V0,V1)
        machine.i = 0x300;
        machine.memory[0x300] = 0b1010_0000;
        machine.v[0] = 0; machine.v[1] = 0;
        machine.step();
        Assert.True(machine.display[0]);
        Assert.False(machine.display[1]);
        Assert.True(machine.display[2]);
        Assert.Equal(0, machine.v[0xF]);

        machine.pc = 0x200;
        machine.step();
        Assert.False(machine.display[0]);
        Assert.Equal(1, machine.v[0xF]);
    }

    [Fact]
    public void drawClipsAtRightAndBottomEdges()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.memory[0x200] = 0xD0; machine.memory[0x201] = 0x11; // DXYN draw 1 row at (V0,V1)
        machine.i = 0x300;
        machine.memory[0x300] = 0b1111_1111;
        machine.v[0] = 62; machine.v[1] = 0;
        machine.step();
        Assert.True(machine.display[62]);
        Assert.True(machine.display[63]);
        Assert.Equal(2, AsciiScreen.litPixelCount(machine.display));
    }

    [Fact]
    public void skipOnKeyStateBothDirections()
    {
        var pressed = TestHarness.withOpcode(0xE09E);
        pressed.v[0] = 0x5; pressed.pressKey(0x5); pressed.step();
        Assert.Equal(0x204, pressed.pc);

        var notPressed = TestHarness.withOpcode(0xE0A1);
        notPressed.v[0] = 0x5; notPressed.step();
        Assert.Equal(0x204, notPressed.pc);
    }

    [Fact]
    public void awaitKeyBlocksUntilReleaseThenStores()
    {
        var machine = TestHarness.withOpcode(0xF00A);
        machine.step();
        Assert.Equal(0x200, machine.pc);
        Assert.True(machine.waitingForKey);

        machine.pressKey(0x9);
        machine.step();
        Assert.Equal(0x200, machine.pc);

        machine.releaseKey(0x9);
        machine.step();
        Assert.Equal(0x202, machine.pc);
        Assert.Equal(0x9, machine.v[0]);
        Assert.False(machine.waitingForKey);
    }
}
