using System;
using System.Collections.Generic;
using System.Text;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class TraceRecorder
{
    readonly List<TraceEntry> entryList = new();

    public IReadOnlyList<TraceEntry> entries => entryList;

    public int count => entryList.Count;

    public bool haltedOnSpin { get; private set; }

    public TraceEntry this[int index] => entryList[index];

    public static TraceRecorder record(byte[] program, int maxSteps, Platform platform = Platform.Chip8, Quirks? quirks = null, int seed = 1, bool stopOnSpin = true)
    {
        var machine = new Machine(quirks, seed, platform);
        machine.loadRom(program);
        var recorder = new TraceRecorder();
        recorder.capture(machine, maxSteps, stopOnSpin);
        return recorder;
    }

    public void capture(Machine machine, int maxSteps, bool stopOnSpin = true)
    {
        int spinRun = 0;

        for (int step = 0; step < maxSteps; step++)
        {
            if (machine.exited)
            {
                break;
            }

            ushort address = machine.pc;
            ushort opcode = (ushort)((machine.memory[address] << 8) | machine.memory[address + 1]);
            ushort? extraWord = null;
            if (opcode == 0xF000)
            {
                extraWord = (ushort)((machine.memory[address + 2] << 8) | machine.memory[address + 3]);
            }

            string mnemonic = Disassembler.describe(opcode, extraWord ?? 0);
            var registers = (byte[])machine.v.Clone();
            entryList.Add(new TraceEntry(step, address, opcode, extraWord, mnemonic, registers, machine.i, machine.sp, machine.delayTimer, machine.soundTimer));

            machine.step();

            if (stopOnSpin && machine.pc == address)
            {
                spinRun++;
                if (spinRun >= 2)
                {
                    haltedOnSpin = true;
                    break;
                }
            }
            else
            {
                spinRun = 0;
            }
        }
    }

    public IReadOnlyList<ushort> visitedAddresses()
    {
        var visited = new List<ushort>();
        var seen = new HashSet<ushort>();
        foreach (var entry in entryList)
        {
            if (seen.Add(entry.address))
            {
                visited.Add(entry.address);
            }
        }
        return visited;
    }

    public string render()
    {
        var builder = new StringBuilder();
        foreach (var entry in entryList)
        {
            builder.Append(entry).Append('\n');
        }
        return builder.ToString();
    }
}
