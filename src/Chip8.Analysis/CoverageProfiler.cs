using System.Collections.Generic;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class CoverageProfiler
{
    public CoverageProfiler(ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public CoverageReport profile(byte[] program, int maxSteps, Platform platform = Platform.Chip8, Quirks? quirks = null, int seed = 1, bool stopOnSpin = true)
    {
        var cfg = ControlFlowGraph.build(program, entry, baseAddress);
        var reachable = new SortedSet<ushort>(cfg.reachableAddresses);

        var machine = new Machine(quirks, seed, platform);
        machine.loadRom(program);

        var executionCounts = new Dictionary<ushort, int>();
        var opcodeHistogram = new Dictionary<ushort, int>();
        int steps = 0;
        int spinRun = 0;
        bool haltedOnSpin = false;

        for (int index = 0; index < maxSteps; index++)
        {
            if (machine.exited)
            {
                break;
            }

            ushort address = machine.pc;
            ushort opcode = (ushort)((machine.memory[address] << 8) | machine.memory[address + 1]);

            increment(executionCounts, address);
            increment(opcodeHistogram, opcode);
            steps++;

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

        return new CoverageReport(steps, haltedOnSpin, executionCounts, opcodeHistogram, reachable);
    }

    static void increment(Dictionary<ushort, int> counter, ushort key)
    {
        counter[key] = counter.TryGetValue(key, out int value) ? value + 1 : 1;
    }
}
