using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chip8.Analysis;

public sealed class ControlFlowGraph
{
    readonly Dictionary<ushort, BasicBlock> blockByAddress;
    readonly List<ControlFlowEdge> edgeList;
    readonly FlowTraversal traversal;

    ControlFlowGraph(
        ushort entry,
        ushort baseAddress,
        Dictionary<ushort, BasicBlock> blockByAddress,
        List<ControlFlowEdge> edgeList,
        FlowTraversal traversal)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
        this.blockByAddress = blockByAddress;
        this.edgeList = edgeList;
        this.traversal = traversal;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public IReadOnlyCollection<BasicBlock> blocks => blockByAddress.Values;
    public IReadOnlyList<ControlFlowEdge> edges => edgeList;
    public IReadOnlyCollection<ushort> leaders => traversal.leaders;
    public IReadOnlyList<ushort> indirectJumps => traversal.indirectJumps;
    public IReadOnlyList<ushort> externalTargets => traversal.externalTargets;

    public int blockCount => blockByAddress.Count;
    public int edgeCount => edgeList.Count;
    public int instructionCount => traversal.decoded.Count;

    public IReadOnlyCollection<ushort> reachableAddresses => traversal.decoded.Keys.ToList();

    public BasicBlock entryBlock => blockByAddress[entry];

    public bool hasBlock(ushort address) => blockByAddress.ContainsKey(address);

    public BasicBlock? blockAt(ushort address) =>
        blockByAddress.TryGetValue(address, out var block) ? block : null;

    public InstructionInfo? instructionAt(ushort address) =>
        traversal.decoded.TryGetValue(address, out var info) ? info : null;

    public bool isReachable(ushort address) => traversal.decoded.ContainsKey(address);

    public IReadOnlyList<BasicBlock> blocksInOrder =>
        blockByAddress.Values.OrderBy(block => block.startAddress).ToList();

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

    public static ControlFlowGraph build(byte[] program, ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        var traversal = FlowTraversal.explore(program, entry, baseAddress);
        var blockByAddress = partition(program, traversal, baseAddress);
        var edgeList = wire(program, blockByAddress, baseAddress);
        return new ControlFlowGraph(entry, baseAddress, blockByAddress, edgeList, traversal);
    }

    static Dictionary<ushort, BasicBlock> partition(byte[] program, FlowTraversal traversal, ushort baseAddress)
    {
        var blockByAddress = new Dictionary<ushort, BasicBlock>();
        var decoded = traversal.decoded;

        foreach (ushort leader in traversal.leaders)
        {
            if (!decoded.ContainsKey(leader))
            {
                continue;
            }

            var block = new BasicBlock(leader);
            ushort address = leader;
            while (true)
            {
                var info = decoded[address];
                block.add(info);

                if (info.isTerminator)
                {
                    break;
                }

                ushort next = info.nextAddress;
                if (!decoded.ContainsKey(next) || traversal.leaders.Contains(next))
                {
                    break;
                }
                address = next;
            }

            blockByAddress[leader] = block;
        }

        return blockByAddress;
    }

    static List<ControlFlowEdge> wire(byte[] program, Dictionary<ushort, BasicBlock> blockByAddress, ushort baseAddress)
    {
        var edgeList = new List<ControlFlowEdge>();

        foreach (var block in blockByAddress.Values)
        {
            var last = block.terminator;
            switch (last.flow)
            {
                case FlowEffect.Jump:
                    link(blockByAddress, edgeList, block, last.jumpTarget!.Value, EdgeKind.Jump);
                    break;

                case FlowEffect.Branch:
                {
                    ushort notTaken = last.nextAddress;
                    ushort taken = (ushort)(notTaken + InstructionDecoder.byteLengthAt(program, notTaken, baseAddress));
                    link(blockByAddress, edgeList, block, notTaken, EdgeKind.Fallthrough);
                    link(blockByAddress, edgeList, block, taken, EdgeKind.BranchTaken);
                    break;
                }

                case FlowEffect.Call:
                    link(blockByAddress, edgeList, block, last.callTarget!.Value, EdgeKind.Call);
                    link(blockByAddress, edgeList, block, last.nextAddress, EdgeKind.Fallthrough);
                    break;

                case FlowEffect.Fallthrough:
                    link(blockByAddress, edgeList, block, last.nextAddress, EdgeKind.Fallthrough);
                    break;

                case FlowEffect.Return:
                case FlowEffect.Terminate:
                case FlowEffect.IndirectJump:
                    break;
            }
        }

        return edgeList;
    }

    static void link(
        Dictionary<ushort, BasicBlock> blockByAddress,
        List<ControlFlowEdge> edgeList,
        BasicBlock source,
        ushort target,
        EdgeKind kind)
    {
        if (!blockByAddress.TryGetValue(target, out var targetBlock))
        {
            return;
        }
        var edge = new ControlFlowEdge(source.startAddress, target, kind);
        source.addSuccessor(edge);
        targetBlock.addPredecessor(source.startAddress);
        edgeList.Add(edge);
    }

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("control flow graph: entry 0x").Append(entry.ToString("X4", CultureInfo.InvariantCulture));
        builder.Append(", ").Append(blockCount).Append(" blocks, ").Append(edgeCount).Append(" edges\n");
        foreach (var block in blocksInOrder)
        {
            builder.Append(block.render());
        }
        return builder.ToString();
    }
}
