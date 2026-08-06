using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class SelfModifyingCodeReport
{
    public SelfModifyingCodeReport(IReadOnlyList<SmcEvent> events, IReadOnlyList<InstructionInfo> staticWriters, IReadOnlyList<ushort> staticTargets)
    {
        this.events = events;
        this.staticWriters = staticWriters;
        this.staticTargets = staticTargets;
    }

    public IReadOnlyList<SmcEvent> events { get; }
    public IReadOnlyList<InstructionInfo> staticWriters { get; }
    public IReadOnlyList<ushort> staticTargets { get; }

    public bool detected => events.Count > 0;
    public bool hasStaticSuspicion => staticWriters.Count > 0;

    public IReadOnlyList<ushort> modifiedAddresses =>
        events.Select(item => item.targetAddress).Distinct().OrderBy(value => value).ToList();

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("self-modifying code: ").Append(detected ? "detected" : "none").Append('\n');
        foreach (var smcEvent in events)
        {
            builder.Append("  ").Append(smcEvent).Append('\n');
        }
        return builder.ToString();
    }
}

public sealed class SelfModifyingCodeDetector
{
    public SelfModifyingCodeDetector(ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public SelfModifyingCodeReport detect(byte[] program, int maxSteps, Platform platform = Platform.Chip8, Quirks? quirks = null, int seed = 1, bool stopOnSpin = true)
    {
        var cfg = ControlFlowGraph.build(program, entry, baseAddress);
        var watched = cfg.reachableByteAddresses();
        var staticFindings = staticSuspects(cfg, watched);

        var machine = new Machine(quirks, seed, platform);
        machine.loadRom(program);

        var snapshot = new Dictionary<ushort, byte>();
        foreach (ushort address in watched)
        {
            snapshot[address] = machine.memory[address];
        }

        var events = new List<SmcEvent>();
        int spinRun = 0;

        for (int step = 0; step < maxSteps; step++)
        {
            if (machine.exited)
            {
                break;
            }

            ushort writerAddress = machine.pc;
            machine.step();

            foreach (ushort address in watched)
            {
                byte current = machine.memory[address];
                if (current != snapshot[address])
                {
                    events.Add(new SmcEvent(step, writerAddress, address, snapshot[address], current));
                    snapshot[address] = current;
                }
            }

            if (stopOnSpin && machine.pc == writerAddress)
            {
                spinRun++;
                if (spinRun >= 2)
                {
                    break;
                }
            }
            else
            {
                spinRun = 0;
            }
        }

        var staticTargets = staticFindings.targets;
        return new SelfModifyingCodeReport(events, staticFindings.writers, staticTargets);
    }

    static (IReadOnlyList<InstructionInfo> writers, IReadOnlyList<ushort> targets) staticSuspects(ControlFlowGraph cfg, HashSet<ushort> codeBytes)
    {
        var writers = new List<InstructionInfo>();
        var indexTargets = new SortedSet<ushort>();

        foreach (var block in cfg.blocksInOrder)
        {
            foreach (var info in block.instructions)
            {
                if (info.loadsIndex && info.indexValue.HasValue && codeBytes.Contains(info.indexValue.Value))
                {
                    indexTargets.Add(info.indexValue.Value);
                }
                if (info.writesMemory)
                {
                    writers.Add(info);
                }
            }
        }

        bool codeTargeted = indexTargets.Count > 0;
        var flaggedWriters = codeTargeted ? writers : new List<InstructionInfo>();
        return (flaggedWriters, indexTargets.ToList());
    }
}
