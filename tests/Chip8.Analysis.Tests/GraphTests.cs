using System.Linq;
using Xunit;

namespace Chip8.Analysis.Tests;

public class GraphTests
{
    [Fact]
    public void controlFlowGraphSplitsBlocksAtLeadersAndTerminators()
    {
        ControlFlowGraph cfg = ControlFlowGraph.build(Programs.branchy);

        Assert.Equal(9, cfg.instructionCount);
        Assert.Equal(
            new ushort[] { 0x200, 0x204, 0x206, 0x208, 0x20A, 0x20C, 0x210 },
            cfg.blocksInOrder.Select(block => block.startAddress).ToArray());
        Assert.Equal(9, cfg.edgeCount);

        BasicBlock entry = cfg.entryBlock;
        Assert.Equal(2, entry.instructionCount);
        Assert.True(entry.isBranch);
        Assert.Equal(
            new[] { (0x204, EdgeKind.Fallthrough), (0x206, EdgeKind.BranchTaken) },
            entry.successors.Select(edge => ((int)edge.target, edge.kind)).ToArray());

        BasicBlock subroutine = cfg.blockAt(0x20C)!;
        Assert.True(subroutine.isReturn);
        Assert.True(subroutine.isTerminal);
        Assert.Equal(new ushort[] { 0x206, 0x208 }, subroutine.predecessors.OrderBy(address => address).ToArray());
        Assert.Equal(4, subroutine.byteLength);

        Assert.Equal(new ushort[] { 0x204, 0x206 }, cfg.blockAt(0x208)!.predecessors.OrderBy(address => address).ToArray());
        Assert.True(cfg.blockAt(0x210)!.isUnconditionalJump);
        Assert.Contains(cfg.edges, edge => edge.source == 0x210 && edge.target == 0x210 && edge.kind == EdgeKind.Jump);
        Assert.Empty(cfg.indirectJumps);
        Assert.Empty(cfg.externalTargets);
        Assert.StartsWith("control flow graph: entry 0x0200, 7 blocks, 9 edges\n", cfg.render());
    }

    [Fact]
    public void indirectJumpsAndTargetsOutsideTheRomAreReported()
    {
        byte[] program = Programs.assemble("SE V0, 0\nJP V0, 0x300\nJP 0x400");
        ControlFlowGraph cfg = ControlFlowGraph.build(program);

        Assert.Equal(new ushort[] { 0x202 }, cfg.indirectJumps.ToArray());
        Assert.Equal(new ushort[] { 0x400 }, cfg.externalTargets.ToArray());
        Assert.True(cfg.blockAt(0x202)!.hasIndirectExit);
        Assert.False(cfg.isReachable(0x400));
    }

    [Fact]
    public void callGraphFindsSubroutinesAndCallers()
    {
        CallGraph graph = CallGraph.build(Programs.branchy);

        Assert.Equal(1, graph.subroutineCount);
        CallNode entry = graph.nodeAt(0x200)!;
        CallNode subroutine = graph.nodeAt(0x20C)!;
        Assert.True(entry.isEntry);
        Assert.Equal(new ushort[] { 0x20C }, entry.callees.ToArray());
        Assert.Equal(new ushort[] { 0x200 }, subroutine.callers.ToArray());
        Assert.True(subroutine.isLeaf);
        Assert.Empty(graph.recursiveSubroutines);
        Assert.Empty(graph.orphanedSubroutines);
        Assert.Equal(new[] { ((ushort)0x200, (ushort)0x20C) }, graph.callEdges.ToArray());
    }

    [Fact]
    public void callGraphDetectsMutualRecursionAndOrphanedTargets()
    {
        CallGraph graph = CallGraph.build(Programs.recursive);

        Assert.Equal(2, graph.subroutineCount);
        Assert.Equal(new ushort[] { 0x204, 0x208 }, graph.recursiveSubroutines.Select(node => node.address).ToArray());
        Assert.False(graph.nodeAt(0x204)!.isSelfRecursive);
        Assert.False(graph.nodeAt(0x200)!.isRecursive);
        Assert.Empty(graph.leafSubroutines);
        Assert.Equal(new ushort[] { 0x210 }, graph.orphanedSubroutines.ToArray());
        Assert.Contains("sub@0x0204 (recursive)", graph.render());
        Assert.Contains("orphaned targets:\n    0x0210\n", graph.render());
    }

    [Fact]
    public void orphanedTargetsComeFromUnreachableCodeOnly()
    {
        byte[] program = Programs.dataBetweenCode;
        CallGraph graph = CallGraph.build(program);
        ReachabilitySet reachability = ReachabilitySet.build(program);

        Assert.Equal(0x1207, InstructionDecoder.decodeAt(program, 0x202)!.opcode);
        Assert.Equal(0x220B, InstructionDecoder.decodeAt(program, 0x204)!.opcode);
        Assert.Equal(0x2217, InstructionDecoder.decodeAt(program, 0x213)!.opcode);
        Assert.True(reachability.contains(0x20B));
        Assert.False(reachability.contains(0x213));
        Assert.Equal(
            new ushort[] { 0x204, 0x213, 0x215, 0x217, 0x219 },
            reachability.unreachableInstructions(program).Select(info => info.address).ToArray());

        Assert.Equal(new ushort[] { 0x20D }, graph.nodeAt(0x200)!.callees.ToArray());
        Assert.Equal(new ushort[] { 0x217 }, graph.orphanedSubroutines.ToArray());
        Assert.Contains("orphaned targets:\n    0x0217\n", graph.render());
    }

    [Fact]
    public void dataWordsCallingOutsideTheRomAreNotOrphans()
    {
        byte[] program = Programs.assemble("halt:\nJP halt\nDB 0x24, 0x18, 0x3C, 0x2F, 0xFF");
        CallGraph graph = CallGraph.build(program);

        Assert.Empty(graph.orphanedSubroutines);
    }

    [Fact]
    public void reachabilitySeparatesCodeFromDataAndDeadCode()
    {
        ReachabilitySet reachability = ReachabilitySet.build(Programs.spriteWithDeadCode);

        Assert.Equal(5, reachability.reachableInstructionCount);
        Assert.Equal(10, reachability.reachableByteCount);
        Assert.Equal(15, reachability.programByteCount);
        Assert.Equal(10.0 / 15.0, reachability.reachableByteFraction, 9);
        Assert.Equal(new ushort[] { 0x20A, 0x20B, 0x20C, 0x20D, 0x20E }, reachability.dataByteAddresses().ToArray());
        Assert.Equal(new ushort[] { 0x20A, 0x20C }, reachability.deadInstructions(Programs.spriteWithDeadCode).Select(info => info.address).ToArray());
        Assert.True(reachability.contains(0x208));
        Assert.False(reachability.contains(0x20A));
    }

    [Fact]
    public void fullyReachableProgramHasNoData()
    {
        ReachabilitySet reachability = ReachabilitySet.build(Programs.branchy);

        Assert.Equal(1.0, reachability.reachableByteFraction);
        Assert.Empty(reachability.dataByteAddresses());
        Assert.Empty(reachability.deadInstructions(Programs.branchy));
    }
}
