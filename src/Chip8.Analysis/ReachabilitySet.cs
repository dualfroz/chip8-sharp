using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chip8.Analysis;

public sealed class ReachabilitySet
{
    readonly SortedSet<ushort> reachable;
    readonly FlowTraversal traversal;

    ReachabilitySet(ushort entry, ushort baseAddress, int programLength, FlowTraversal traversal)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
        this.programLength = programLength;
        this.traversal = traversal;
        reachable = new SortedSet<ushort>(traversal.decoded.Keys);
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }
    public int programLength { get; }

    public IReadOnlyCollection<ushort> reachableAddresses => reachable;

    public int reachableInstructionCount => reachable.Count;

    public bool contains(ushort address) => reachable.Contains(address);

    public int reachableByteCount
    {
        get
        {
            int total = 0;
            foreach (var info in traversal.decoded.Values)
            {
                total += info.byteLength;
            }
            return total;
        }
    }

    public int programByteCount => programLength;

    public double reachableByteFraction =>
        programLength == 0 ? 0.0 : (double)reachableByteCount / programLength;

    public HashSet<ushort> reachableByteAddresses()
    {
        var bytes = new HashSet<ushort>();
        foreach (var info in traversal.decoded.Values)
        {
            for (int offset = 0; offset < info.byteLength; offset++)
            {
                bytes.Add((ushort)(info.address + offset));
            }
        }
        return bytes;
    }

    public IReadOnlyList<InstructionInfo> deadInstructions(byte[] program)
    {
        var linear = InstructionDecoder.decodeLinear(program, baseAddress);
        return linear.Where(info => !reachable.Contains(info.address)).ToList();
    }

    public IReadOnlyList<ushort> dataByteAddresses()
    {
        var covered = reachableByteAddresses();
        var data = new List<ushort>();
        for (int offset = 0; offset < programLength; offset++)
        {
            ushort address = (ushort)(baseAddress + offset);
            if (!covered.Contains(address))
            {
                data.Add(address);
            }
        }
        return data;
    }

    public static ReachabilitySet build(byte[] program, ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        var traversal = FlowTraversal.explore(program, entry, baseAddress);
        return new ReachabilitySet(entry, baseAddress, program.Length, traversal);
    }

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("reachable instructions: ").Append(reachableInstructionCount);
        builder.Append(" (").Append(reachableByteCount).Append(" of ").Append(programLength).Append(" bytes)\n");
        foreach (ushort address in reachable)
        {
            builder.Append("  0x").Append(address.ToString("X4", CultureInfo.InvariantCulture)).Append('\n');
        }
        return builder.ToString();
    }
}
