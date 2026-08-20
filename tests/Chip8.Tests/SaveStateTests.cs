using System.IO;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class SaveStateTests
{
    [Fact]
    public void loadStateRestoresRegistersMemoryAndDisplay()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x7001, 0x1200);
        machine.step();
        machine.v[3] = 0xAB;
        machine.i = 0x321;
        machine.memory[0x400] = 0x42;
        machine.display[5] = true;

        byte[] state = machine.saveState();

        machine.v[3] = 0x00;
        machine.i = 0x000;
        machine.memory[0x400] = 0x99;
        machine.display[5] = false;
        machine.pc = 0x2FF;

        machine.loadState(state);

        Assert.Equal(0xAB, machine.v[3]);
        Assert.Equal(0x321, machine.i);
        Assert.Equal(0x42, machine.memory[0x400]);
        Assert.True(machine.display[5]);
        Assert.Equal(0x202, machine.pc);
    }

    [Fact]
    public void restoreBuildsANewMachineFromState()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x6009);
        machine.step();
        byte[] state = machine.saveState();

        Machine restored = Machine.restore(state);

        Assert.Equal(Platform.SuperChip, restored.platform);
        Assert.Equal(machine.v[0], restored.v[0]);
        Assert.Equal(machine.pc, restored.pc);
    }

    [Fact]
    public void restoredMachineExecutesIdenticallyToTheOriginal()
    {
        var original = TestHarness.withProgram(Platform.Chip8, 0x7001, 0x1200);
        for (int step = 0; step < 20; step++)
        {
            original.step();
        }

        Machine resumed = Machine.restore(original.saveState());
        for (int step = 0; step < 15; step++)
        {
            original.step();
            resumed.step();
        }

        Assert.Equal(original.pc, resumed.pc);
        Assert.Equal(original.v[0], resumed.v[0]);
        Assert.Equal(original.i, resumed.i);
    }

    [Fact]
    public void xoChipStatePreservesPlanesAndAudio()
    {
        var machine = TestHarness.withProgram(Platform.XoChip, 0xF201, 0xA400, 0xD001);
        machine.memory[0x400] = 0x80;
        machine.audioPattern[3] = 0xC3;
        for (int step = 0; step < 3; step++)
        {
            machine.step();
        }
        Assert.Equal(2, machine.video.colorAt(0, 0));

        Machine restored = Machine.restore(machine.saveState());

        Assert.Equal(2, restored.planeMask);
        Assert.Equal(2, restored.video.colorAt(0, 0));
        Assert.Equal(0xC3, restored.audioPattern[3]);
    }

    [Fact]
    public void loadStateRejectsAMismatchedPlatform()
    {
        var chip8 = new Machine(Quirks.modern(), 1);
        byte[] state = chip8.saveState();
        var xoChip = new Machine(null, 1, Platform.XoChip);
        Assert.Throws<InvalidDataException>(() => xoChip.loadState(state));
    }

    [Fact]
    public void restoreRejectsGarbage()
    {
        Assert.Throws<InvalidDataException>(() => Machine.restore(new byte[] { 1, 2, 3, 4, 5 }));
    }
}
