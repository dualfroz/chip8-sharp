using System.Globalization;

namespace Chip8.Analysis;

public readonly struct OpcodeFields
{
    public OpcodeFields(ushort opcode)
    {
        this.opcode = opcode;
        x = (opcode & 0x0F00) >> 8;
        y = (opcode & 0x00F0) >> 4;
        n = opcode & 0x000F;
        nn = opcode & 0x00FF;
        nnn = (ushort)(opcode & 0x0FFF);
        highNibble = (opcode & 0xF000) >> 12;
    }

    public ushort opcode { get; }
    public int highNibble { get; }
    public int x { get; }
    public int y { get; }
    public int n { get; }
    public int nn { get; }
    public ushort nnn { get; }

    public override string ToString()
    {
        return "0x" + opcode.ToString("X4", CultureInfo.InvariantCulture)
            + " x=" + x + " y=" + y + " n=" + n
            + " nn=0x" + nn.ToString("X2", CultureInfo.InvariantCulture)
            + " nnn=0x" + nnn.ToString("X3", CultureInfo.InvariantCulture);
    }
}
