using System;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class MachineTests
{
    [Fact]
    public void resetLoadsFontAndProgramCounter()
    {
        var machine = new Machine(Quirks.modern(), 1);
        Assert.Equal(Machine.programStart, machine.pc);
        Assert.Equal(0xF0, machine.memory[0x50]);
        Assert.Equal(0x80, machine.memory[0x50 + 5 * 0xF + 4]);
    }

    [Fact]
    public void loadRomCopiesBytesToProgramArea()
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.loadRom(new byte[] { 0x12, 0x34, 0x56 });
        Assert.Equal(0x12, machine.memory[0x200]);
        Assert.Equal(0x34, machine.memory[0x201]);
        Assert.Equal(0x56, machine.memory[0x202]);
    }

    [Fact]
    public void loadRomRejectsOversizedImage()
    {
        var machine = new Machine(Quirks.modern(), 1);
        Assert.Throws<ArgumentException>(() => machine.loadRom(new byte[Machine.memorySize]));
    }

    [Fact]
    public void delayAndSoundTimersCountDownAndGateBeep()
    {
        var machine = TestHarness.withOpcode(0xF018);
        machine.v[0] = 3;
        machine.step();
        Assert.Equal(3, machine.soundTimer);
        Assert.True(machine.soundActive);

        machine.tickTimers();
        machine.tickTimers();
        machine.tickTimers();
        Assert.Equal(0, machine.soundTimer);
        Assert.False(machine.soundActive);

        machine.tickTimers();
        Assert.Equal(0, machine.soundTimer);
    }

    [Fact]
    public void readDelayTimerIntoRegister()
    {
        var machine = TestHarness.withOpcode(0xF007);
        machine.delayTimer = 0x2A;
        machine.step();
        Assert.Equal(0x2A, machine.v[0]);
    }

    [Fact]
    public void addToIndex()
    {
        var machine = TestHarness.withOpcode(0xF01E);
        machine.i = 0x200; machine.v[0] = 0x05;
        machine.step();
        Assert.Equal(0x205, machine.i);
    }

    [Fact]
    public void fontCharacterAddress()
    {
        var machine = TestHarness.withOpcode(0xF029);
        machine.v[0] = 0xB;
        machine.step();
        Assert.Equal(0x50 + 0xB * 5, machine.i);
        Assert.Equal(0xE0, machine.memory[machine.i]);
    }

    [Fact]
    public void binaryCodedDecimal()
    {
        var machine = TestHarness.withOpcode(0xF033);
        machine.i = 0x300; machine.v[0] = 234;
        machine.step();
        Assert.Equal(2, machine.memory[0x300]);
        Assert.Equal(3, machine.memory[0x301]);
        Assert.Equal(4, machine.memory[0x302]);
    }

    [Fact]
    public void storeAndLoadRegistersHonourIncrementQuirk()
    {
        var modern = TestHarness.withOpcode(0xF255, Quirks.modern());
        modern.i = 0x300;
        modern.v[0] = 0xAA; modern.v[1] = 0xBB; modern.v[2] = 0xCC;
        modern.step();
        Assert.Equal(0xAA, modern.memory[0x300]);
        Assert.Equal(0xBB, modern.memory[0x301]);
        Assert.Equal(0xCC, modern.memory[0x302]);
        Assert.Equal(0x300, modern.i);

        var cosmac = TestHarness.withOpcode(0xF265, Quirks.cosmacVip());
        cosmac.i = 0x300;
        cosmac.memory[0x300] = 0x11; cosmac.memory[0x301] = 0x22;
        cosmac.step();
        Assert.Equal(0x11, cosmac.v[0]);
        Assert.Equal(0x22, cosmac.v[1]);
        Assert.Equal(0x303, cosmac.i);
    }

    [Fact]
    public void systemAddressOpcodeIsInert()
    {
        var machine = TestHarness.withOpcode(0x0123);
        machine.step();
        Assert.Equal(0x202, machine.pc);
    }
}
