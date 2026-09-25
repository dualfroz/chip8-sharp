using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Chip8.Core;

namespace Chip8.OctoAsm;

public sealed class OctoProgram
{
    const int dataBytesPerLine = 8;

    readonly HashSet<int> instructionStarts;

    public OctoProgram(
        byte[] rom,
        IReadOnlyDictionary<string, int> labels,
        IReadOnlyDictionary<string, double> constants,
        IReadOnlyDictionary<string, int> breakpoints,
        IEnumerable<int> instructionAddresses)
    {
        this.rom = rom;
        this.labels = labels;
        this.constants = constants;
        this.breakpoints = breakpoints;
        instructionStarts = new HashSet<int>(instructionAddresses);
    }

    public byte[] rom { get; }
    public IReadOnlyDictionary<string, int> labels { get; }
    public IReadOnlyDictionary<string, double> constants { get; }
    public IReadOnlyDictionary<string, int> breakpoints { get; }

    public ushort baseAddress => OctoImage.origin;

    public IReadOnlyCollection<int> instructionAddresses => instructionStarts;

    public int labelAddress(string name) => labels[name];

    public bool isInstruction(int address) => instructionStarts.Contains(address);

    public string renderListing()
    {
        var labelsByAddress = labels
            .GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).OrderBy(name => name).ToList());

        var builder = new StringBuilder();
        int end = baseAddress + rom.Length;
        int address = baseAddress;

        while (address < end)
        {
            if (labelsByAddress.TryGetValue(address, out List<string>? names))
            {
                foreach (string name in names)
                {
                    builder.Append("                ").Append(name).Append(":\n");
                }
            }

            if (instructionStarts.Contains(address) && address + 1 < end)
            {
                ushort opcode = word(address);
                bool longLoad = Disassembler.isLongLoad(opcode) && address + 3 < end;
                ushort extra = longLoad ? word(address + 2) : (ushort)0;
                string bytes = opcode.ToString("X4", CultureInfo.InvariantCulture)
                    + (longLoad ? " " + extra.ToString("X4", CultureInfo.InvariantCulture) : "");
                builder.Append(addressColumn(address)).Append(bytes.PadRight(10)).Append(Disassembler.describe(opcode, extra)).Append('\n');
                address += longLoad ? 4 : 2;
                continue;
            }

            int start = address;
            var data = new List<string>();
            while (address < end && data.Count < dataBytesPerLine && !instructionStarts.Contains(address)
                && (address == start || !labelsByAddress.ContainsKey(address)))
            {
                data.Add("0x" + rom[address - baseAddress].ToString("X2", CultureInfo.InvariantCulture));
                address++;
            }
            if (data.Count == 0)
            {
                data.Add("0x" + rom[address - baseAddress].ToString("X2", CultureInfo.InvariantCulture));
                address++;
            }
            builder.Append(addressColumn(start)).Append("          DB ").Append(string.Join(", ", data)).Append('\n');
        }

        return builder.ToString();
    }

    ushort word(int address) => (ushort)((rom[address - baseAddress] << 8) | rom[address - baseAddress + 1]);

    static string addressColumn(int address) => "0x" + address.ToString("X4", CultureInfo.InvariantCulture) + ":  ";
}
