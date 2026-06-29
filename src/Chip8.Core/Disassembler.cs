using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chip8.Core;

public readonly struct DecodedInstruction
{
    public DecodedInstruction(ushort address, ushort opcode, ushort? extraWord, int byteLength, string mnemonic)
    {
        this.address = address;
        this.opcode = opcode;
        this.extraWord = extraWord;
        this.byteLength = byteLength;
        this.mnemonic = mnemonic;
    }

    public ushort address { get; }
    public ushort opcode { get; }
    public ushort? extraWord { get; }
    public int byteLength { get; }
    public string mnemonic { get; }

    public override string ToString()
    {
        string bytes = extraWord.HasValue
            ? opcode.ToString("X4", CultureInfo.InvariantCulture) + " " + extraWord.Value.ToString("X4", CultureInfo.InvariantCulture)
            : opcode.ToString("X4", CultureInfo.InvariantCulture);
        return "0x" + address.ToString("X4", CultureInfo.InvariantCulture) + ": " + bytes.PadRight(9) + " " + mnemonic;
    }
}

public static class Disassembler
{
    public static bool isLongLoad(ushort opcode) => opcode == 0xF000;

    public static string describe(ushort opcode, ushort extraWord = 0)
    {
        int x = (opcode & 0x0F00) >> 8;
        int y = (opcode & 0x00F0) >> 4;
        int n = opcode & 0x000F;
        int nn = opcode & 0x00FF;
        int nnn = opcode & 0x0FFF;

        return (opcode & 0xF000) switch
        {
            0x0000 => describeSystem(opcode, n, nnn),
            0x1000 => "JP 0x" + hex3(nnn),
            0x2000 => "CALL 0x" + hex3(nnn),
            0x3000 => "SE V" + hex1(x) + ", 0x" + hex2(nn),
            0x4000 => "SNE V" + hex1(x) + ", 0x" + hex2(nn),
            0x5000 => describeSkipRegister(opcode, x, y, n),
            0x6000 => "LD V" + hex1(x) + ", 0x" + hex2(nn),
            0x7000 => "ADD V" + hex1(x) + ", 0x" + hex2(nn),
            0x8000 => describeArithmetic(opcode, x, y, n),
            0x9000 => n == 0 ? "SNE V" + hex1(x) + ", V" + hex1(y) : dataWord(opcode),
            0xA000 => "LD I, 0x" + hex3(nnn),
            0xB000 => "JP V0, 0x" + hex3(nnn),
            0xC000 => "RND V" + hex1(x) + ", 0x" + hex2(nn),
            0xD000 => "DRW V" + hex1(x) + ", V" + hex1(y) + ", " + n,
            0xE000 => describeKey(opcode, x, nn),
            0xF000 => describeMisc(opcode, x, nn, extraWord),
            _ => dataWord(opcode),
        };
    }

    public static IReadOnlyList<DecodedInstruction> disassemble(byte[] program, ushort baseAddress = 0x200)
    {
        var lines = new List<DecodedInstruction>();
        int offset = 0;

        while (offset + 1 < program.Length)
        {
            ushort address = (ushort)(baseAddress + offset);
            ushort opcode = (ushort)((program[offset] << 8) | program[offset + 1]);

            if (isLongLoad(opcode) && offset + 3 < program.Length)
            {
                ushort extraWord = (ushort)((program[offset + 2] << 8) | program[offset + 3]);
                lines.Add(new DecodedInstruction(address, opcode, extraWord, 4, describe(opcode, extraWord)));
                offset += 4;
            }
            else
            {
                lines.Add(new DecodedInstruction(address, opcode, null, 2, describe(opcode)));
                offset += 2;
            }
        }

        return lines;
    }

    public static string render(byte[] program, ushort baseAddress = 0x200)
    {
        var builder = new StringBuilder();
        foreach (var line in disassemble(program, baseAddress))
        {
            builder.Append(line).Append('\n');
        }
        return builder.ToString();
    }

    static string describeSystem(ushort opcode, int n, int nnn)
    {
        if ((opcode & 0xFFF0) == 0x00C0)
        {
            return "SCD " + n;
        }
        if ((opcode & 0xFFF0) == 0x00D0)
        {
            return "SCU " + n;
        }
        return opcode switch
        {
            0x00E0 => "CLS",
            0x00EE => "RET",
            0x00FB => "SCR",
            0x00FC => "SCL",
            0x00FD => "EXIT",
            0x00FE => "LORES",
            0x00FF => "HIRES",
            _ => "SYS 0x" + hex3(nnn),
        };
    }

    static string describeSkipRegister(ushort opcode, int x, int y, int n) => n switch
    {
        0x0 => "SE V" + hex1(x) + ", V" + hex1(y),
        0x2 => "SAVE V" + hex1(x) + "-V" + hex1(y),
        0x3 => "LOAD V" + hex1(x) + "-V" + hex1(y),
        _ => dataWord(opcode),
    };

    static string describeArithmetic(ushort opcode, int x, int y, int n) => n switch
    {
        0x0 => "LD V" + hex1(x) + ", V" + hex1(y),
        0x1 => "OR V" + hex1(x) + ", V" + hex1(y),
        0x2 => "AND V" + hex1(x) + ", V" + hex1(y),
        0x3 => "XOR V" + hex1(x) + ", V" + hex1(y),
        0x4 => "ADD V" + hex1(x) + ", V" + hex1(y),
        0x5 => "SUB V" + hex1(x) + ", V" + hex1(y),
        0x6 => "SHR V" + hex1(x),
        0x7 => "SUBN V" + hex1(x) + ", V" + hex1(y),
        0xE => "SHL V" + hex1(x),
        _ => dataWord(opcode),
    };

    static string describeKey(ushort opcode, int x, int nn) => nn switch
    {
        0x9E => "SKP V" + hex1(x),
        0xA1 => "SKNP V" + hex1(x),
        _ => dataWord(opcode),
    };

    static string describeMisc(ushort opcode, int x, int nn, ushort extraWord)
    {
        if (opcode == 0xF000)
        {
            return "LD I, 0x" + extraWord.ToString("X4", CultureInfo.InvariantCulture);
        }
        if ((opcode & 0xF0FF) == 0xF001)
        {
            return "PLANE " + x;
        }
        if (opcode == 0xF002)
        {
            return "AUDIO";
        }

        return nn switch
        {
            0x07 => "LD V" + hex1(x) + ", DT",
            0x0A => "LD V" + hex1(x) + ", K",
            0x15 => "LD DT, V" + hex1(x),
            0x18 => "LD ST, V" + hex1(x),
            0x1E => "ADD I, V" + hex1(x),
            0x29 => "LD F, V" + hex1(x),
            0x30 => "LD HF, V" + hex1(x),
            0x33 => "LD B, V" + hex1(x),
            0x3A => "PITCH V" + hex1(x),
            0x55 => "LD [I], V" + hex1(x),
            0x65 => "LD V" + hex1(x) + ", [I]",
            0x75 => "LD R, V" + hex1(x),
            0x85 => "LD V" + hex1(x) + ", R",
            _ => dataWord(opcode),
        };
    }

    static string dataWord(ushort opcode) => "DW 0x" + opcode.ToString("X4", CultureInfo.InvariantCulture);

    static string hex1(int value) => value.ToString("X1", CultureInfo.InvariantCulture);

    static string hex2(int value) => value.ToString("X2", CultureInfo.InvariantCulture);

    static string hex3(int value) => value.ToString("X3", CultureInfo.InvariantCulture);
}
