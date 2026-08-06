using System;
using System.Globalization;
using System.Text;
using Chip8.Core;

namespace Chip8.Analysis;

public sealed class TraceEntry
{
    public TraceEntry(int step, ushort address, ushort opcode, ushort? extraWord, string mnemonic, byte[] registers, ushort index, byte stackPointer, byte delayTimer, byte soundTimer)
    {
        this.step = step;
        this.address = address;
        this.opcode = opcode;
        this.extraWord = extraWord;
        this.mnemonic = mnemonic;
        this.registers = registers;
        this.index = index;
        this.stackPointer = stackPointer;
        this.delayTimer = delayTimer;
        this.soundTimer = soundTimer;
    }

    public int step { get; }
    public ushort address { get; }
    public ushort opcode { get; }
    public ushort? extraWord { get; }
    public string mnemonic { get; }
    public byte[] registers { get; }
    public ushort index { get; }
    public byte stackPointer { get; }
    public byte delayTimer { get; }
    public byte soundTimer { get; }

    public byte register(int slot) => registers[slot & 0xF];

    public string registerDump()
    {
        var builder = new StringBuilder();
        for (int slot = 0; slot < registers.Length; slot++)
        {
            if (slot > 0)
            {
                builder.Append(' ');
            }
            builder.Append('V').Append(slot.ToString("X1", CultureInfo.InvariantCulture));
            builder.Append('=').Append(registers[slot].ToString("X2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    public override string ToString()
    {
        return "#" + step.ToString(CultureInfo.InvariantCulture).PadLeft(5)
            + "  0x" + address.ToString("X4", CultureInfo.InvariantCulture)
            + "  " + opcode.ToString("X4", CultureInfo.InvariantCulture)
            + "  " + mnemonic.PadRight(18)
            + "  I=0x" + index.ToString("X3", CultureInfo.InvariantCulture)
            + "  SP=" + stackPointer.ToString(CultureInfo.InvariantCulture);
    }
}
