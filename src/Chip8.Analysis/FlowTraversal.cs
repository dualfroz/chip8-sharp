using System.Collections.Generic;

namespace Chip8.Analysis;

public sealed class FlowTraversal
{
    FlowTraversal(
        ushort entry,
        ushort baseAddress,
        IReadOnlyDictionary<ushort, InstructionInfo> decoded,
        IReadOnlyCollection<ushort> leaders,
        IReadOnlyList<ushort> indirectJumps,
        IReadOnlyList<ushort> externalTargets)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
        this.decoded = decoded;
        this.leaders = leaders;
        this.indirectJumps = indirectJumps;
        this.externalTargets = externalTargets;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }
    public IReadOnlyDictionary<ushort, InstructionInfo> decoded { get; }
    public IReadOnlyCollection<ushort> leaders { get; }
    public IReadOnlyList<ushort> indirectJumps { get; }
    public IReadOnlyList<ushort> externalTargets { get; }

    public static FlowTraversal explore(byte[] program, ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        var decoded = new Dictionary<ushort, InstructionInfo>();
        var leaders = new SortedSet<ushort> { entry };
        var indirectJumps = new List<ushort>();
        var externalTargets = new SortedSet<ushort>();
        var pending = new Queue<ushort>();
        var seen = new HashSet<ushort>();
        pending.Enqueue(entry);

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
                externalTargets.Add(address);
                continue;
            }

            decoded[address] = info;

            switch (info.flow)
            {
                case FlowEffect.Fallthrough:
                    enqueue(pending, info.nextAddress);
                    break;

                case FlowEffect.Branch:
                {
                    ushort notTaken = info.nextAddress;
                    ushort taken = (ushort)(notTaken + InstructionDecoder.byteLengthAt(program, notTaken, baseAddress));
                    leaders.Add(notTaken);
                    leaders.Add(taken);
                    enqueue(pending, notTaken);
                    enqueue(pending, taken);
                    break;
                }

                case FlowEffect.Jump:
                    leaders.Add(info.jumpTarget!.Value);
                    enqueue(pending, info.jumpTarget!.Value);
                    break;

                case FlowEffect.Call:
                {
                    ushort target = info.callTarget!.Value;
                    ushort returnAddress = info.nextAddress;
                    leaders.Add(target);
                    leaders.Add(returnAddress);
                    enqueue(pending, target);
                    enqueue(pending, returnAddress);
                    break;
                }

                case FlowEffect.IndirectJump:
                    indirectJumps.Add(address);
                    break;

                case FlowEffect.Return:
                case FlowEffect.Terminate:
                    break;
            }
        }

        var indirectList = new List<ushort>(indirectJumps);
        indirectList.Sort();
        return new FlowTraversal(entry, baseAddress, decoded, leaders, indirectList, new List<ushort>(externalTargets));
    }

    static void enqueue(Queue<ushort> pending, ushort address) => pending.Enqueue(address);
}
