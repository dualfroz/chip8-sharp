using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public sealed class AssemblyResult
{
    public AssemblyResult(byte[] rom, ushort baseAddress, IReadOnlyDictionary<string, int> symbols, string listing)
    {
        this.rom = rom;
        this.baseAddress = baseAddress;
        this.symbols = symbols;
        this.listing = listing;
    }

    public byte[] rom { get; }
    public ushort baseAddress { get; }
    public IReadOnlyDictionary<string, int> symbols { get; }
    public string listing { get; }
}
