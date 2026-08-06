using System.Globalization;

namespace Chip8.Analysis;

public enum EdgeKind
{
    Fallthrough,
    Jump,
    BranchTaken,
    Call,
}

public readonly struct ControlFlowEdge
{
    public ControlFlowEdge(ushort source, ushort target, EdgeKind kind)
    {
        this.source = source;
        this.target = target;
        this.kind = kind;
    }

    public ushort source { get; }
    public ushort target { get; }
    public EdgeKind kind { get; }

    public override string ToString() =>
        "0x" + source.ToString("X4", CultureInfo.InvariantCulture)
        + " -> 0x" + target.ToString("X4", CultureInfo.InvariantCulture)
        + " (" + kind + ")";
}
