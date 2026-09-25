using System;

namespace Chip8.OctoAsm;

sealed class OctoImage
{
    public const int origin = 0x200;
    public const int addressLimit = 0x10000;

    readonly byte[] memory = new byte[addressLimit];

    public int here { get; set; } = origin;
    public int highest { get; private set; } = origin;

    public byte this[int address]
    {
        get => memory[address];
        set => memory[address] = value;
    }

    public void emit(int value, SourcePosition where)
    {
        if (here >= addressLimit)
        {
            throw new OctoCompileException(
                new OctoDiagnostic(OctoSeverity.Error, "program does not fit in 64 KB of memory", where));
        }
        memory[here++] = (byte)value;
        highest = Math.Max(highest, here);
    }

    public void emitWord(int value, SourcePosition where)
    {
        emit(value >> 8, where);
        emit(value & 0xFF, where);
    }

    public void patchAddress(int address, int target)
    {
        memory[address] = (byte)((memory[address] & 0xF0) | ((target >> 8) & 0x0F));
        memory[address + 1] = (byte)(target & 0xFF);
    }

    public byte[] toRom()
    {
        var rom = new byte[highest - origin];
        Array.Copy(memory, origin, rom, 0, rom.Length);
        return rom;
    }
}
