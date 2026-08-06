using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chip8.Analysis;

public sealed class SpriteScanner
{
    public SpriteScanner(ushort entry = InstructionDecoder.defaultBaseAddress, ushort baseAddress = InstructionDecoder.defaultBaseAddress)
    {
        this.entry = entry;
        this.baseAddress = baseAddress;
    }

    public ushort entry { get; }
    public ushort baseAddress { get; }

    public IReadOnlyList<SpriteCandidate> scan(byte[] program)
    {
        var traversal = FlowTraversal.explore(program, entry, baseAddress);
        var ordered = traversal.decoded.Values.OrderBy(info => info.address).ToList();

        var candidates = new List<SpriteCandidate>();
        ushort? lastIndex = null;
        ushort lastIndexAddress = 0;

        foreach (var info in ordered)
        {
            if (info.loadsIndex && info.indexValue.HasValue)
            {
                lastIndex = info.indexValue.Value;
                lastIndexAddress = info.address;
                continue;
            }

            if (info.kind == InstructionKind.LoadFont || info.kind == InstructionKind.LoadBigFont || info.kind == InstructionKind.AddIndex)
            {
                lastIndex = null;
                continue;
            }

            if (info.kind == InstructionKind.Draw && lastIndex.HasValue)
            {
                int rows = info.spriteRows!.Value;
                int bytesPerRow = info.n == 0 ? 2 : 1;
                byte[] bytes = readBytes(program, lastIndex.Value, rows * bytesPerRow);
                candidates.Add(new SpriteCandidate(lastIndex.Value, rows, bytesPerRow, bytes, lastIndexAddress, info.address));
            }
        }

        return candidates;
    }

    public IReadOnlyList<SpriteCandidate> scanDistinct(byte[] program)
    {
        var seen = new HashSet<ushort>();
        var distinct = new List<SpriteCandidate>();
        foreach (var candidate in scan(program))
        {
            if (seen.Add(candidate.address))
            {
                distinct.Add(candidate);
            }
        }
        return distinct;
    }

    public SpriteCandidate scanRegion(byte[] program, ushort address, int rows, int bytesPerRow = 1)
    {
        byte[] bytes = readBytes(program, address, rows * bytesPerRow);
        return new SpriteCandidate(address, rows, bytesPerRow, bytes, 0, 0);
    }

    byte[] readBytes(byte[] program, ushort address, int count)
    {
        int offset = address - baseAddress;
        var bytes = new byte[count];
        for (int index = 0; index < count; index++)
        {
            int source = offset + index;
            bytes[index] = source >= 0 && source < program.Length ? program[source] : (byte)0;
        }
        return bytes;
    }

    public string render(IReadOnlyList<SpriteCandidate> candidates)
    {
        var builder = new StringBuilder();
        foreach (var candidate in candidates)
        {
            builder.Append(candidate).Append('\n');
            builder.Append(candidate.renderAscii());
        }
        return builder.ToString();
    }
}
