using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chip8.Analysis;

public sealed class BasicBlock
{
    readonly List<InstructionInfo> instructionList = new();
    readonly List<ControlFlowEdge> successorList = new();
    readonly List<ushort> predecessorList = new();

    public BasicBlock(ushort startAddress)
    {
        this.startAddress = startAddress;
    }

    public ushort startAddress { get; }

    public IReadOnlyList<InstructionInfo> instructions => instructionList;
    public IReadOnlyList<ControlFlowEdge> successors => successorList;
    public IReadOnlyList<ushort> predecessors => predecessorList;

    public InstructionInfo terminator => instructionList[instructionList.Count - 1];

    public InstructionInfo first => instructionList[0];

    public ushort endAddress => terminator.address;

    public ushort nextAddress => terminator.nextAddress;

    public int byteLength
    {
        get
        {
            int total = 0;
            foreach (var instruction in instructionList)
            {
                total += instruction.byteLength;
            }
            return total;
        }
    }

    public int instructionCount => instructionList.Count;

    public bool isReturn => terminator.flow == FlowEffect.Return;

    public bool isCall => terminator.flow == FlowEffect.Call;

    public bool isBranch => terminator.flow == FlowEffect.Branch;

    public bool isUnconditionalJump => terminator.flow == FlowEffect.Jump;

    public bool hasIndirectExit => terminator.flow == FlowEffect.IndirectJump;

    public bool isTerminal => successorList.Count == 0;

    internal void add(InstructionInfo instruction) => instructionList.Add(instruction);

    internal void addSuccessor(ControlFlowEdge edge) => successorList.Add(edge);

    internal void addPredecessor(ushort predecessor)
    {
        if (!predecessorList.Contains(predecessor))
        {
            predecessorList.Add(predecessor);
        }
    }

    public string render()
    {
        var builder = new StringBuilder();
        builder.Append("block 0x").Append(startAddress.ToString("X4", CultureInfo.InvariantCulture));
        builder.Append(" (").Append(instructionCount).Append(" instr, ").Append(byteLength).Append(" bytes)\n");
        foreach (var instruction in instructionList)
        {
            builder.Append("  ").Append(instruction).Append('\n');
        }
        foreach (var edge in successorList)
        {
            builder.Append("  => ").Append(edge).Append('\n');
        }
        return builder.ToString();
    }

    public override string ToString() =>
        "block@0x" + startAddress.ToString("X4", CultureInfo.InvariantCulture)
        + " [" + instructionCount + " instr]";
}
