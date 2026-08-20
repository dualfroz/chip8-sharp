using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class WatchpointTests
{
    [Fact]
    public void memoryWatchStopsWhenTheByteChanges()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0xA400, 0x6005, 0xF055, 0x1206);
        var debugger = new Debugger(machine);
        debugger.watchMemory(0x400);

        RunResult outcome = debugger.run(100);

        Assert.Equal(StopReason.Watchpoint, outcome.reason);
        Assert.Equal(3, outcome.steps);
        Assert.Equal(0x400, outcome.triggeredAddress);
        Assert.Equal(5, machine.memory[0x400]);
        Assert.Contains("memory 0x0400", debugger.lastWatchDescription);
    }

    [Fact]
    public void registerWatchStopsWhenTheRegisterChanges()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x6005, 0x1202);
        var debugger = new Debugger(machine);
        debugger.watchRegister(0);

        RunResult outcome = debugger.run(50);

        Assert.Equal(StopReason.Watchpoint, outcome.reason);
        Assert.Equal(1, outcome.steps);
        Assert.Contains("V0", debugger.lastWatchDescription);
    }

    [Fact]
    public void unwatchedMemoryDoesNotStop()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0xA400, 0x6005, 0xF055, 0x1206);
        var debugger = new Debugger(machine);
        debugger.watchMemory(0x400);
        debugger.clearWatches();

        RunResult outcome = debugger.run(20);

        Assert.Equal(StopReason.StepLimit, outcome.reason);
    }

    [Fact]
    public void stepOverRunsThroughACall()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x2204, 0x6042, 0x0000, 0x00EE);
        var debugger = new Debugger(machine);

        RunResult outcome = debugger.stepOver(100);

        Assert.Equal(0x202, machine.pc);
        Assert.Equal(0, machine.v[0]);
        Assert.Equal(3, outcome.steps);
    }

    [Fact]
    public void stepOverIsASingleStepForNonCalls()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x6005, 0x1202);
        var debugger = new Debugger(machine);

        debugger.stepOver(100);

        Assert.Equal(0x202, machine.pc);
        Assert.Equal(5, machine.v[0]);
    }

    [Fact]
    public void runUntilStopsAtTheTargetAddress()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x7001, 0x1200);
        var debugger = new Debugger(machine);

        RunResult outcome = debugger.runUntil(0x202, 100);

        Assert.Equal(0x202, machine.pc);
        Assert.Equal(StopReason.Breakpoint, outcome.reason);
    }

    [Fact]
    public void disassembleWindowDecodesFromProgramCounter()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0xA300, 0x6001, 0x00EE);
        var debugger = new Debugger(machine);

        var lines = debugger.disassembleWindow(3);

        Assert.Equal(3, lines.Count);
        Assert.Equal(0x200, lines[0].address);
        Assert.Equal("LD I, 0x300", lines[0].mnemonic);
        Assert.Equal("RET", lines[2].mnemonic);
    }
}
