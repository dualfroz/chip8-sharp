using System.Collections.Generic;
using System.Globalization;

namespace Chip8.Analysis;

public sealed class CallNode
{
    readonly SortedSet<ushort> calleeSet = new();
    readonly SortedSet<ushort> callerSet = new();

    public CallNode(ushort address, bool isEntry)
    {
        this.address = address;
        this.isEntry = isEntry;
    }

    public ushort address { get; }
    public bool isEntry { get; }
    public bool isRecursive { get; internal set; }

    public IReadOnlyCollection<ushort> callees => calleeSet;
    public IReadOnlyCollection<ushort> callers => callerSet;

    public int outDegree => calleeSet.Count;
    public int inDegree => callerSet.Count;

    public bool isLeaf => calleeSet.Count == 0;
    public bool isSelfRecursive => calleeSet.Contains(address);

    internal void addCallee(ushort callee) => calleeSet.Add(callee);

    internal void addCaller(ushort caller) => callerSet.Add(caller);

    public override string ToString()
    {
        string label = "sub@0x" + address.ToString("X4", CultureInfo.InvariantCulture);
        if (isEntry)
        {
            label += " (entry)";
        }
        if (isRecursive)
        {
            label += " (recursive)";
        }
        return label;
    }
}
