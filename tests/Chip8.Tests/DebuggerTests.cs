using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class DebuggerTests
{
    [Fact]
    public void stepAdvancesProgramCounter()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x6001);
        var debugger = new Debugger(machine);
        debugger.step();
        Assert.Equal(0x202, machine.pc);
        Assert.Equal(1, machine.v[0]);
    }

    [Fact]
    public void peekAndDisassembleShowUpcomingInstruction()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0xA300);
        var debugger = new Debugger(machine);
        Assert.Equal(0xA300, debugger.peekOpcode());
        Assert.Equal("LD I, 0x300", debugger.disassembleNext());
    }

    [Fact]
    public void breakpointStopsRunBeforeExecuting()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x6001, 0x6102, 0x6203, 0x1200);
        var debugger = new Debugger(machine);
        debugger.addBreakpoint(0x204);

        var outcome = debugger.run(100);

        Assert.Equal(StopReason.Breakpoint, outcome.reason);
        Assert.Equal(2, outcome.steps);
        Assert.Equal(0x204, machine.pc);
        Assert.Equal(1, machine.v[0]);
        Assert.Equal(2, machine.v[1]);
        Assert.Equal(0, machine.v[2]);
    }

    [Fact]
    public void runWithoutBreakpointReachesStepLimit()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x1200);
        var debugger = new Debugger(machine);
        var outcome = debugger.run(50);
        Assert.Equal(StopReason.StepLimit, outcome.reason);
        Assert.Equal(50, outcome.steps);
    }

    [Fact]
    public void runStopsWhenInterpreterExits()
    {
        var machine = TestHarness.withProgram(Platform.SuperChip, 0x6001, 0x00FD);
        var debugger = new Debugger(machine);
        var outcome = debugger.run(100);
        Assert.Equal(StopReason.Exited, outcome.reason);
        Assert.True(machine.exited);
    }

    [Fact]
    public void snapshotCapturesRegistersStackAndTimers()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x2204);
        var debugger = new Debugger(machine);
        machine.v[5] = 0x9A;
        machine.delayTimer = 7;

        debugger.step();
        var snapshot = debugger.snapshot();

        Assert.Equal(0x204, snapshot.pc);
        Assert.Equal(1, snapshot.sp);
        Assert.Single(snapshot.stack);
        Assert.Equal(0x202, snapshot.stack[0]);
        Assert.Equal(0x9A, snapshot.v[5]);
        Assert.Equal(7, snapshot.delayTimer);
        Assert.Contains("PC=0x0204", snapshot.format());
        Assert.Contains("V5=9A", snapshot.format());
    }

    [Fact]
    public void snapshotIsNotAliasedToLiveState()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x6001);
        var debugger = new Debugger(machine);
        var snapshot = debugger.snapshot();
        machine.v[0] = 0xFF;
        Assert.Equal(0, snapshot.v[0]);
    }

    [Fact]
    public void readMemoryReturnsRequestedWindow()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0xABCD);
        var debugger = new Debugger(machine);
        var window = debugger.readMemory(0x200, 2);
        Assert.Equal(0xAB, window[0]);
        Assert.Equal(0xCD, window[1]);
    }

    [Fact]
    public void breakpointCanBeRemoved()
    {
        var machine = TestHarness.withProgram(Platform.Chip8, 0x1200);
        var debugger = new Debugger(machine);
        debugger.addBreakpoint(0x200);
        Assert.True(debugger.hasBreakpoint(0x200));
        debugger.removeBreakpoint(0x200);
        Assert.False(debugger.hasBreakpoint(0x200));
    }
}
