using System;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class XoChipTests
{
    [Fact]
    public void planeSelectStoresMask()
    {
        var machine = TestHarness.withOpcode(0xF201, Platform.XoChip);
        machine.step();
        Assert.Equal(2, machine.planeMask);
    }

    [Fact]
    public void drawToSecondPlaneProducesColorTwo()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF201, 0xD011);
        machine.i = 0x300;
        machine.memory[0x300] = 0x80;
        machine.step();
        machine.step();
        Assert.Equal(2, machine.video.colorAt(0, 0));
    }

    [Fact]
    public void drawToBothPlanesProducesColorThree()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF301, 0xD011);
        machine.i = 0x300;
        machine.memory[0x300] = 0x80;
        machine.memory[0x301] = 0x80;
        machine.step();
        machine.step();
        Assert.Equal(3, machine.video.colorAt(0, 0));
        Assert.Equal(0, machine.video.colorAt(1, 0));
    }

    [Fact]
    public void clearAffectsOnlySelectedPlane()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF301, 0xD011, 0xF101, 0x00E0);
        machine.i = 0x300;
        machine.memory[0x300] = 0x80;
        machine.memory[0x301] = 0x80;
        machine.step();
        machine.step();
        machine.step();
        machine.step();
        Assert.Equal(2, machine.video.colorAt(0, 0));
    }

    [Fact]
    public void longLoadSetsIndexToSixteenBitAddress()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF000, 0x1234);
        machine.step();
        Assert.Equal(0x1234, machine.i);
        Assert.Equal(0x204, machine.pc);
    }

    [Fact]
    public void takenSkipJumpsOverLongLoad()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x3000, 0xF000, 0x1234, 0x00E0);
        machine.step();
        Assert.Equal(0x206, machine.pc);
    }

    [Fact]
    public void takenSkipOverNormalInstructionAdvancesTwo()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x3000, 0x6042);
        machine.step();
        Assert.Equal(0x204, machine.pc);
    }

    [Fact]
    public void storeAndLoadRegisterRange()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x5132, 0x5463);
        machine.i = 0x400;
        machine.v[1] = 0xAA;
        machine.v[2] = 0xBB;
        machine.v[3] = 0xCC;

        machine.step();
        Assert.Equal(0xAA, machine.memory[0x400]);
        Assert.Equal(0xBB, machine.memory[0x401]);
        Assert.Equal(0xCC, machine.memory[0x402]);
        Assert.Equal(0x400, machine.i);

        machine.step();
        Assert.Equal(0xAA, machine.v[4]);
        Assert.Equal(0xBB, machine.v[5]);
        Assert.Equal(0xCC, machine.v[6]);
    }

    [Fact]
    public void storeRangeInReverseOrder()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x5312);
        machine.i = 0x400;
        machine.v[1] = 0x11;
        machine.v[2] = 0x22;
        machine.v[3] = 0x33;

        machine.step();

        Assert.Equal(0x33, machine.memory[0x400]);
        Assert.Equal(0x22, machine.memory[0x401]);
        Assert.Equal(0x11, machine.memory[0x402]);
    }

    [Fact]
    public void audioPatternBufferLoadsFromMemory()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF002);
        machine.i = 0x400;
        for (int offset = 0; offset < 16; offset++)
        {
            machine.memory[0x400 + offset] = (byte)offset;
        }

        machine.step();

        Assert.Equal(0, machine.audioPattern[0]);
        Assert.Equal(15, machine.audioPattern[15]);
    }

    [Fact]
    public void pitchSetsPlaybackFrequency()
    {
        var machine = TestHarness.withOpcode(0xF03A, Platform.XoChip);
        machine.v[0] = 112;
        machine.step();
        Assert.Equal(112, machine.audioPitch);
        Assert.True(Math.Abs(machine.audioFrequency - 8000.0) < 0.001);
    }

    [Fact]
    public void extendedMemoryIsSixtyFourKilobytes()
    {
        var machine = new Machine(null, 1, Platform.XoChip);
        Assert.Equal(65536, machine.memory.Length);

        var rom = new byte[5000];
        rom[0] = 0xAB;
        rom[4999] = 0xCD;
        machine.loadRom(rom);
        Assert.Equal(0xAB, machine.memory[0x200]);
        Assert.Equal(0xCD, machine.memory[0x200 + 4999]);
    }

    [Fact]
    public void scrollUpOpcodeMovesContent()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0x00D4);
        machine.memory[0x300] = 0x80;
        machine.video.drawSprite(machine.memory, 0x300, 5, 10, 1, 1, false);
        Assert.True(machine.video.pixel(0, 5, 10));

        machine.step();

        Assert.False(machine.video.pixel(0, 5, 10));
        Assert.True(machine.video.pixel(0, 5, 6));
    }
}
