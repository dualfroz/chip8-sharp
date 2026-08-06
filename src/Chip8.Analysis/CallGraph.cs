using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chip8.Analysis;

public sealed class CallGraph
{
    readonly Dictionary<ushort, CallNode> nodeByAddress;
    readonly List<ushort> unreachableSubroutines;

    CallGraph(ushort entry, ushort baseAddress, Dictionary<ushort, CallNode> nodeByAddress, List<ushort> unreachableSubroutines)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
        this.nodeByAddress = nodeByAddress;
        this.unreachableSubroutines = unreachableSubroutines;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public IReadOnlyCollection<CallNode> nodes => nodeByAddress.Values;
    public int nodeCount => nodeByAddress.Count;

    public int subroutineCount => nodeByAddress.Count - (nodeByAddress.ContainsKey(entry) ? 1 : 0);

    public IReadOnlyList<ushort> orphanedSubroutines => unreachableSubroutines;

    public bool hasNode(ushort address) => nodeByAddress.ContainsKey(address);

    public CallNode? nodeAt(ushort address) =>
        nodeByAddress.TryGetValue(address, out var node) ? node : null;

    public IReadOnlyList<CallNode> recursiveSubroutines =>
        nodeByAddress.Values.Where(node => node.isRecursive).OrderBy(node => node.address).ToList();

    public IReadOnlyList<CallNode> leafSubroutines =>
        nodeByAddress.Values.Where(node => node.isLeaf).OrderBy(node => node.address).ToList();

    public IReadOnlyList<CallNode> nodesInOrder =>
        nodeByAddress.Values.OrderBy(node => node.address).ToList();

    public IReadOnlyList<(ushort caller, ushort callee)> callEdges
    {
        get
        {
            var edges = new List<(ushort, ushort)>();
            foreach (var node in nodesInOrder)
            {
                foreach (ushort callee in node.callees)
                {
                    edges.Add((node.address, callee));
                }
            }
            return edges;
        }
    }

    public static CallGraph build(byte[] program, ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        var nodeByAddress = new Dictionary<ushort, CallNode> { [entry] = new CallNode(entry, true) };
        var subroutineQueue = new Queue<ushort>();
        var processed = new HashSet<ushort>();
        subroutineQueue.Enqueue(entry);

        while (subroutineQueue.Count > 0)
        {
            ushort subroutine = subroutineQueue.Dequeue();
            if (!processed.Add(subroutine))
            {
                continue;
            }

            var callerNode = nodeByAddress[subroutine];
            foreach (ushort callee in scanBody(program, subroutine, baseAddress))
            {
                callerNode.addCallee(callee);
                if (!nodeByAddress.TryGetValue(callee, out var calleeNode))
                {
                    calleeNode = new CallNode(callee, false);
                    nodeByAddress[callee] = calleeNode;
                }
                calleeNode.addCaller(subroutine);
                subroutineQueue.Enqueue(callee);
            }
        }

        markRecursion(nodeByAddress);
        var orphans = findOrphans(program, baseAddress, nodeByAddress);
        return new CallGraph(entry, baseAddress, nodeByAddress, orphans);
    }

    static IEnumerable<ushort> scanBody(byte[] program, ushort subroutine, ushort baseAddress)
    {
        var callees = new List<ushort>();
        var pending = new Queue<ushort>();
        var seen = new HashSet<ushort>();
        pending.Enqueue(subroutine);

        while (pending.Count > 0)
        {
            ushort address = pending.Dequeue();
            if (!seen.Add(address))
            {
                continue;
            }

            var info = InstructionDecoder.decodeAt(program, address, baseAddress);
            if (info is null)
            {
                continue;
            }

            switch (info.flow)
            {
                case FlowEffect.Fallthrough:
                    pending.Enqueue(info.nextAddress);
                    break;

                case FlowEffect.Branch:
                    pending.Enqueue(info.nextAddress);
                    pending.Enqueue((ushort)(info.nextAddress + InstructionDecoder.byteLengthAt(program, info.nextAddress, baseAddress)));
                    break;

                case FlowEffect.Jump:
                    pending.Enqueue(info.jumpTarget!.Value);
                    break;

                case FlowEffect.Call:
                    callees.Add(info.callTarget!.Value);
                    pending.Enqueue(info.nextAddress);
                    break;

                case FlowEffect.Return:
                case FlowEffect.Terminate:
                case FlowEffect.IndirectJump:
                    break;
            }
        }

        return callees;
    }

    static void markRecursion(Dictionary<ushort, CallNode> nodeByAddress)
    {
        foreach (var node in nodeByAddress.Values)
        {
            if (reaches(nodeByAddress, node.address, node.address))
            {
                node.isRecursive = true;
            }
        }
    }

    static bool reaches(Dictionary<ushort, CallNode> nodeByAddress, ushort start, ushort goal)
    {
        var pending = new Queue<ushort>();
        var seen = new HashSet<ushort>();
        foreach (ushort callee in nodeByAddress[start].callees)
        {
            pending.Enqueue(callee);
        }

        while (pending.Count > 0)
        {
            ushort current = pending.Dequeue();
            if (current == goal)
            {
                return true;
            }
            if (!seen.Add(current))
            {
                continue;
            }
            if (nodeByAddress.TryGetValue(current, out var node))
            {
                foreach (ushort callee in node.callees)
                {
                    pending.Enqueue(callee);
                }
            }
        }

        return false;
    }

    static List<ushort> findOrphans(byte[] program, ushort baseAddress, Dictionary<ushort, CallNode> nodeByAddress)
    {
        var orphans = new SortedSet<ushort>();
        foreach (var info in InstructionDecoder.decodeLinear(program, baseAddress))
        {
            if (info.kind == InstructionKind.Call)
            {
                ushort target = info.callTarget!.Value;
                if (!nodeByAddress.ContainsKey(target))
                {
                    orphans.Add(target);
                }
            }
        }
        return orphans.ToList();
    }

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("call graph: entry 0x").Append(entry.ToString("X4", CultureInfo.InvariantCulture));
        builder.Append(", ").Append(subroutineCount).Append(" subroutines\n");
        foreach (var node in nodesInOrder)
        {
            builder.Append("  ").Append(node).Append('\n');
            foreach (ushort callee in node.callees)
            {
                builder.Append("    calls 0x").Append(callee.ToString("X4", CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        if (unreachableSubroutines.Count > 0)
        {
            builder.Append("  orphaned targets:\n");
            foreach (ushort orphan in unreachableSubroutines)
            {
                builder.Append("    0x").Append(orphan.ToString("X4", CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        return builder.ToString();
    }
}
