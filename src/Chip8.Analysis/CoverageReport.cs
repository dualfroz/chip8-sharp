using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chip8.Analysis;

public sealed class CoverageReport
{
    readonly Dictionary<ushort, int> executionCounts;
    readonly Dictionary<ushort, int> opcodeHistogram;
    readonly SortedSet<ushort> reachable;

    public CoverageReport(
        int totalSteps,
        bool haltedOnSpin,
        Dictionary<ushort, int> executionCounts,
        Dictionary<ushort, int> opcodeHistogram,
        SortedSet<ushort> reachable)
    {
        this.totalSteps = totalSteps;
        this.haltedOnSpin = haltedOnSpin;
        this.executionCounts = executionCounts;
        this.opcodeHistogram = opcodeHistogram;
        this.reachable = reachable;
    }

    public int totalSteps { get; }
    public bool haltedOnSpin { get; }

    public IReadOnlyDictionary<ushort, int> executionCountByAddress => executionCounts;
    public IReadOnlyDictionary<ushort, int> opcodeCounts => opcodeHistogram;

    public IReadOnlyCollection<ushort> executedAddresses => executionCounts.Keys;
    public IReadOnlyCollection<ushort> reachableAddresses => reachable;

    public int executedInstructionCount => executionCounts.Count;
    public int reachableInstructionCount => reachable.Count;

    public int executionCount(ushort address) =>
        executionCounts.TryGetValue(address, out int value) ? value : 0;

    public IReadOnlyCollection<ushort> coveredReachableAddresses =>
        executionCounts.Keys.Where(address => reachable.Contains(address)).ToList();

    public IReadOnlyCollection<ushort> executedOutsideReachable =>
        executionCounts.Keys.Where(address => !reachable.Contains(address)).OrderBy(address => address).ToList();

    public double coveragePercent
    {
        get
        {
            if (reachable.Count == 0)
            {
                return 0.0;
            }
            int covered = executionCounts.Keys.Count(address => reachable.Contains(address));
            return 100.0 * covered / reachable.Count;
        }
    }

    public IReadOnlyList<ushort> reachableButNotExecuted =>
        reachable.Where(address => !executionCounts.ContainsKey(address)).ToList();

    public (ushort address, int count) hotspot()
    {
        ushort best = 0;
        int bestCount = -1;
        foreach (var pair in executionCounts)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                best = pair.Key;
            }
        }
        return (best, bestCount < 0 ? 0 : bestCount);
    }

    public string renderHeatmap()
    {
        var builder = new StringBuilder();
        builder.Append("coverage: ").Append(coveragePercent.ToString("F1", CultureInfo.InvariantCulture)).Append("% over ");
        builder.Append(totalSteps).Append(" steps\n");
        foreach (var address in executionCounts.Keys.OrderBy(value => value))
        {
            builder.Append("  0x").Append(address.ToString("X4", CultureInfo.InvariantCulture));
            builder.Append("  x").Append(executionCounts[address].ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return builder.ToString();
    }
}
