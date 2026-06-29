using System;
using System.Collections.Generic;

namespace Chip8.Core;

public enum StopReason
{
    StepLimit,
    Breakpoint,
    Watchpoint,
    Exited,
}

public readonly struct RunResult
{
    public RunResult(int steps, StopReason reason, ushort triggeredAddress = 0)
    {
        this.steps = steps;
        this.reason = reason;
        this.triggeredAddress = triggeredAddress;
    }

    public int steps { get; }
    public StopReason reason { get; }
    public ushort triggeredAddress { get; }
}

public sealed class Debugger
{
    readonly HashSet<ushort> breakpoints = new();
    readonly Dictionary<ushort, byte> memoryWatches = new();
    readonly Dictionary<int, byte> registerWatches = new();

    public Debugger(Machine machine)
    {
        this.machine = machine;
    }

    public Machine machine { get; }

    public IReadOnlyCollection<ushort> breakpointAddresses => breakpoints;

    public IReadOnlyCollection<ushort> watchedMemory => memoryWatches.Keys;

    public IReadOnlyCollection<int> watchedRegisters => registerWatches.Keys;

    public string? lastWatchDescription { get; private set; }

    public void addBreakpoint(ushort address) => breakpoints.Add(address);

    public void removeBreakpoint(ushort address) => breakpoints.Remove(address);

    public void clearBreakpoints() => breakpoints.Clear();

    public bool hasBreakpoint(ushort address) => breakpoints.Contains(address);

    public void watchMemory(ushort address) => memoryWatches[address] = machine.memory[address & (machine.memory.Length - 1)];

    public void unwatchMemory(ushort address) => memoryWatches.Remove(address);

    public void watchRegister(int register) => registerWatches[register & 0xF] = machine.v[register & 0xF];

    public void unwatchRegister(int register) => registerWatches.Remove(register & 0xF);

    public void clearWatches()
    {
        memoryWatches.Clear();
        registerWatches.Clear();
    }

    public ushort peekOpcode() => (ushort)((machine.memory[machine.pc] << 8) | machine.memory[machine.pc + 1]);

    public string disassembleNext()
    {
        ushort opcode = peekOpcode();
        ushort extraWord = Disassembler.isLongLoad(opcode)
            ? (ushort)((machine.memory[machine.pc + 2] << 8) | machine.memory[machine.pc + 3])
            : (ushort)0;
        return Disassembler.describe(opcode, extraWord);
    }

    public IReadOnlyList<DecodedInstruction> disassembleWindow(int count)
    {
        var lines = new List<DecodedInstruction>();
        int address = machine.pc;

        for (int taken = 0; taken < count && address + 1 < machine.memory.Length; taken++)
        {
            ushort opcode = (ushort)((machine.memory[address] << 8) | machine.memory[address + 1]);
            if (Disassembler.isLongLoad(opcode) && address + 3 < machine.memory.Length)
            {
                ushort extra = (ushort)((machine.memory[address + 2] << 8) | machine.memory[address + 3]);
                lines.Add(new DecodedInstruction((ushort)address, opcode, extra, 4, Disassembler.describe(opcode, extra)));
                address += 4;
            }
            else
            {
                lines.Add(new DecodedInstruction((ushort)address, opcode, null, 2, Disassembler.describe(opcode)));
                address += 2;
            }
        }

        return lines;
    }

    public void step() => machine.step();

    public RunResult run(int maxSteps)
    {
        for (int taken = 0; taken < maxSteps; taken++)
        {
            if (machine.exited)
            {
                return new RunResult(taken, StopReason.Exited);
            }
            if (taken > 0 && breakpoints.Contains(machine.pc))
            {
                return new RunResult(taken, StopReason.Breakpoint, machine.pc);
            }

            machine.step();

            if (checkWatches(out ushort hitAddress))
            {
                return new RunResult(taken + 1, StopReason.Watchpoint, hitAddress);
            }
        }

        return new RunResult(maxSteps, machine.exited ? StopReason.Exited : StopReason.StepLimit);
    }

    public RunResult runUntil(ushort address, int maxSteps)
    {
        for (int taken = 0; taken < maxSteps; taken++)
        {
            if (machine.exited)
            {
                return new RunResult(taken, StopReason.Exited);
            }
            machine.step();
            if (machine.pc == address)
            {
                return new RunResult(taken + 1, StopReason.Breakpoint, address);
            }
            if (checkWatches(out ushort hitAddress))
            {
                return new RunResult(taken + 1, StopReason.Watchpoint, hitAddress);
            }
        }

        return new RunResult(maxSteps, machine.exited ? StopReason.Exited : StopReason.StepLimit);
    }

    public RunResult stepOver(int maxSteps)
    {
        ushort opcode = peekOpcode();
        if ((opcode & 0xF000) == 0x2000)
        {
            ushort returnAddress = (ushort)(machine.pc + 2);
            return runUntil(returnAddress, maxSteps);
        }

        machine.step();
        return new RunResult(1, machine.exited ? StopReason.Exited : StopReason.StepLimit);
    }

    bool checkWatches(out ushort hitAddress)
    {
        hitAddress = 0;
        bool triggered = false;

        foreach (ushort address in new List<ushort>(memoryWatches.Keys))
        {
            byte current = machine.memory[address & (machine.memory.Length - 1)];
            if (current != memoryWatches[address])
            {
                if (!triggered)
                {
                    hitAddress = address;
                    lastWatchDescription = "memory 0x" + address.ToString("X4") + " changed to 0x" + current.ToString("X2");
                    triggered = true;
                }
                memoryWatches[address] = current;
            }
        }

        foreach (int register in new List<int>(registerWatches.Keys))
        {
            byte current = machine.v[register];
            if (current != registerWatches[register])
            {
                if (!triggered)
                {
                    lastWatchDescription = "register V" + register.ToString("X1") + " changed to 0x" + current.ToString("X2");
                    triggered = true;
                }
                registerWatches[register] = current;
            }
        }

        return triggered;
    }

    public byte[] readMemory(ushort address, int length)
    {
        var window = new byte[length];
        for (int offset = 0; offset < length; offset++)
        {
            window[offset] = machine.memory[(address + offset) & (machine.memory.Length - 1)];
        }
        return window;
    }

    public MachineSnapshot snapshot() => MachineSnapshot.capture(machine);
}
