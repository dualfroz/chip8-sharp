using System.Collections.Generic;
using System.Linq;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class RomAnalyzer
{
    static readonly HashSet<InstructionKind> superChipMarkers = new()
    {
        InstructionKind.ScrollRight,
        InstructionKind.ScrollLeft,
        InstructionKind.ScrollDown,
        InstructionKind.Exit,
        InstructionKind.LowRes,
        InstructionKind.HiRes,
        InstructionKind.LoadBigFont,
        InstructionKind.StoreFlags,
        InstructionKind.LoadFlags,
    };

    static readonly HashSet<InstructionKind> xoChipMarkers = new()
    {
        InstructionKind.LoadLong,
        InstructionKind.Plane,
        InstructionKind.Audio,
        InstructionKind.Pitch,
        InstructionKind.SaveRange,
        InstructionKind.LoadRange,
        InstructionKind.ScrollUp,
    };

    public RomAnalyzer(ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public RomProfile analyze(byte[] program)
    {
        var cfg = ControlFlowGraph.build(program, entry, baseAddress);
        var callGraph = CallGraph.build(program, entry, baseAddress);

        var histogram = new Dictionary<InstructionKind, int>();
        var superFeatures = new SortedSet<InstructionKind>();
        var xoFeatures = new SortedSet<InstructionKind>();
        int drawCount = 0;
        int callCount = 0;
        int jumpCount = 0;
        int skipCount = 0;

        foreach (var block in cfg.blocksInOrder)
        {
            foreach (var info in block.instructions)
            {
                histogram[info.kind] = histogram.TryGetValue(info.kind, out int value) ? value + 1 : 1;

                if (info.kind == InstructionKind.Draw)
                {
                    drawCount++;
                    if (info.n == 0)
                    {
                        superFeatures.Add(InstructionKind.Draw);
                    }
                }
                else if (info.kind == InstructionKind.Call)
                {
                    callCount++;
                }
                else if (info.kind == InstructionKind.Jump)
                {
                    jumpCount++;
                }
                else if (info.category == InstructionCategory.Skip)
                {
                    skipCount++;
                }

                if (superChipMarkers.Contains(info.kind))
                {
                    superFeatures.Add(info.kind);
                }
                if (xoChipMarkers.Contains(info.kind))
                {
                    xoFeatures.Add(info.kind);
                }
            }
        }

        bool usesXoChip = xoFeatures.Count > 0;
        bool usesSuperChip = superFeatures.Count > 0;
        Platform guessed = usesXoChip
            ? Platform.XoChip
            : usesSuperChip ? Platform.SuperChip : Platform.Chip8;

        return new RomProfile(
            program.Length,
            entry,
            cfg.instructionCount,
            cfg.blockCount,
            callGraph.subroutineCount,
            drawCount,
            callCount,
            jumpCount,
            skipCount,
            usesSuperChip,
            usesXoChip,
            guessed,
            histogram,
            superFeatures.ToList(),
            xoFeatures.ToList());
    }
}
