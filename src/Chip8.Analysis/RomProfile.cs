using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class RomProfile
{
    public RomProfile(
        int size,
        ushort entry,
        int instructionCount,
        int blockCount,
        int subroutineCount,
        int drawCount,
        int callCount,
        int jumpCount,
        int skipCount,
        bool usesSuperChip,
        bool usesXoChip,
        Platform guessedPlatform,
        IReadOnlyDictionary<InstructionKind, int> kindHistogram,
        IReadOnlyList<InstructionKind> superChipFeatures,
        IReadOnlyList<InstructionKind> xoChipFeatures)
    {
        this.size = size;
        this.entry = entry;
        this.instructionCount = instructionCount;
        this.blockCount = blockCount;
        this.subroutineCount = subroutineCount;
        this.drawCount = drawCount;
        this.callCount = callCount;
        this.jumpCount = jumpCount;
        this.skipCount = skipCount;
        this.usesSuperChip = usesSuperChip;
        this.usesXoChip = usesXoChip;
        this.guessedPlatform = guessedPlatform;
        this.kindHistogram = kindHistogram;
        this.superChipFeatures = superChipFeatures;
        this.xoChipFeatures = xoChipFeatures;
    }

    public int size { get; }
    public ushort entry { get; }
    public int instructionCount { get; }
    public int blockCount { get; }
    public int subroutineCount { get; }
    public int drawCount { get; }
    public int callCount { get; }
    public int jumpCount { get; }
    public int skipCount { get; }
    public bool usesSuperChip { get; }
    public bool usesXoChip { get; }
    public Platform guessedPlatform { get; }
    public IReadOnlyDictionary<InstructionKind, int> kindHistogram { get; }
    public IReadOnlyList<InstructionKind> superChipFeatures { get; }
    public IReadOnlyList<InstructionKind> xoChipFeatures { get; }

    public int kindCount(InstructionKind kind) =>
        kindHistogram.TryGetValue(kind, out int value) ? value : 0;

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("rom profile\n");
        builder.Append("  size:          ").Append(size).Append(" bytes\n");
        builder.Append("  entry:         0x").Append(entry.ToString("X4", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append("  instructions:  ").Append(instructionCount).Append('\n');
        builder.Append("  blocks:        ").Append(blockCount).Append('\n');
        builder.Append("  subroutines:   ").Append(subroutineCount).Append('\n');
        builder.Append("  draws:         ").Append(drawCount).Append('\n');
        builder.Append("  calls:         ").Append(callCount).Append('\n');
        builder.Append("  jumps:         ").Append(jumpCount).Append('\n');
        builder.Append("  skips:         ").Append(skipCount).Append('\n');
        builder.Append("  platform:      ").Append(guessedPlatform).Append('\n');
        builder.Append("  histogram:\n");
        foreach (var pair in kindHistogram.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key.ToString()))
        {
            builder.Append("    ").Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
        }
        return builder.ToString();
    }
}
