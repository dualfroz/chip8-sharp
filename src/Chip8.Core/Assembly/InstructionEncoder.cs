using System;
using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public static class InstructionEncoder
{
    public static int measure(InstructionStatement instruction, Func<Token, int> resolver)
    {
        if (isLongLoad(instruction, resolver))
        {
            return 4;
        }
        return 2;
    }

    public static void emit(InstructionStatement instruction, Func<Token, int> resolver, List<byte> output)
    {
        string mnemonic = instruction.mnemonic.ToUpperInvariant();
        IReadOnlyList<Operand> operands = instruction.operands;

        switch (mnemonic)
        {
            case "CLS": zeroOperands(instruction); writeWord(output, 0x00E0); return;
            case "RET": zeroOperands(instruction); writeWord(output, 0x00EE); return;
            case "SCR": zeroOperands(instruction); writeWord(output, 0x00FB); return;
            case "SCL": zeroOperands(instruction); writeWord(output, 0x00FC); return;
            case "EXIT": zeroOperands(instruction); writeWord(output, 0x00FD); return;
            case "LORES":
            case "LOW": zeroOperands(instruction); writeWord(output, 0x00FE); return;
            case "HIRES":
            case "HIGH": zeroOperands(instruction); writeWord(output, 0x00FF); return;
            case "AUDIO": zeroOperands(instruction); writeWord(output, 0xF002); return;

            case "SCD": writeWord(output, (ushort)(0x00C0 | nibble(singleImmediate(instruction, resolver), instruction, "SCD"))); return;
            case "SCU": writeWord(output, (ushort)(0x00D0 | nibble(singleImmediate(instruction, resolver), instruction, "SCU"))); return;
            case "PLANE": writeWord(output, (ushort)(0xF001 | (nibble(singleImmediate(instruction, resolver), instruction, "PLANE") << 8))); return;

            case "JP": encodeJump(instruction, resolver, output); return;
            case "CALL": writeWord(output, (ushort)(0x2000 | address(singleImmediate(instruction, resolver), instruction, "CALL"))); return;

            case "SE": encodeSkip(instruction, resolver, output, 0x3000, 0x5000); return;
            case "SNE": encodeSkip(instruction, resolver, output, 0x4000, 0x9000); return;

            case "OR": encodeArithmetic(instruction, output, 0x1); return;
            case "AND": encodeArithmetic(instruction, output, 0x2); return;
            case "XOR": encodeArithmetic(instruction, output, 0x3); return;
            case "SUB": encodeArithmetic(instruction, output, 0x5); return;
            case "SUBN": encodeArithmetic(instruction, output, 0x7); return;
            case "SHR": encodeShift(instruction, output, 0x6); return;
            case "SHL": encodeShift(instruction, output, 0xE); return;

            case "ADD": encodeAdd(instruction, resolver, output); return;
            case "LD": encodeLoad(instruction, resolver, output); return;

            case "RND":
                requireOperands(instruction, 2);
                writeWord(output, (ushort)(0xC000 | (register(operands[0], "RND") << 8) | byte8(immediate(operands[1], resolver), instruction, "RND")));
                return;

            case "DRW":
                requireOperands(instruction, 3);
                writeWord(output, (ushort)(0xD000 | (register(operands[0], "DRW") << 8) | (register(operands[1], "DRW") << 4) | nibble(immediate(operands[2], resolver), instruction, "DRW")));
                return;

            case "SKP":
                requireOperands(instruction, 1);
                writeWord(output, (ushort)(0xE09E | (register(operands[0], "SKP") << 8)));
                return;

            case "SKNP":
                requireOperands(instruction, 1);
                writeWord(output, (ushort)(0xE0A1 | (register(operands[0], "SKNP") << 8)));
                return;

            case "PITCH":
                requireOperands(instruction, 1);
                writeWord(output, (ushort)(0xF03A | (register(operands[0], "PITCH") << 8)));
                return;

            case "SAVE": encodeRange(instruction, output, 0x2); return;
            case "LOAD": encodeRange(instruction, output, 0x3); return;

            default:
                throw new AsmException("unknown mnemonic '" + instruction.mnemonic + "'", instruction.position);
        }
    }

    static void encodeJump(InstructionStatement instruction, Func<Token, int> resolver, List<byte> output)
    {
        IReadOnlyList<Operand> operands = instruction.operands;
        if (operands.Count == 1)
        {
            writeWord(output, (ushort)(0x1000 | address(immediate(operands[0], resolver), instruction, "JP")));
            return;
        }

        if (operands.Count == 2 && operands[0].isRegister(0))
        {
            writeWord(output, (ushort)(0xB000 | address(immediate(operands[1], resolver), instruction, "JP")));
            return;
        }

        throw new AsmException("JP expects an address or 'V0, address'", instruction.position);
    }

    static void encodeSkip(InstructionStatement instruction, Func<Token, int> resolver, List<byte> output, int immediateBase, int registerBase)
    {
        requireOperands(instruction, 2);
        IReadOnlyList<Operand> operands = instruction.operands;
        int x = register(operands[0], instruction.mnemonic);

        if (operands[1].kind == OperandKind.Register)
        {
            writeWord(output, (ushort)(registerBase | (x << 8) | (operands[1].registerIndex << 4)));
            return;
        }

        writeWord(output, (ushort)(immediateBase | (x << 8) | byte8(immediate(operands[1], resolver), instruction, instruction.mnemonic)));
    }

    static void encodeArithmetic(InstructionStatement instruction, List<byte> output, int op)
    {
        requireOperands(instruction, 2);
        IReadOnlyList<Operand> operands = instruction.operands;
        int x = register(operands[0], instruction.mnemonic);
        int y = register(operands[1], instruction.mnemonic);
        writeWord(output, (ushort)(0x8000 | (x << 8) | (y << 4) | op));
    }

    static void encodeShift(InstructionStatement instruction, List<byte> output, int op)
    {
        IReadOnlyList<Operand> operands = instruction.operands;
        if (operands.Count is < 1 or > 2)
        {
            throw new AsmException(instruction.mnemonic + " expects 'Vx' or 'Vx, Vy'", instruction.position);
        }
        int x = register(operands[0], instruction.mnemonic);
        int y = operands.Count == 2 ? register(operands[1], instruction.mnemonic) : 0;
        writeWord(output, (ushort)(0x8000 | (x << 8) | (y << 4) | op));
    }

    static void encodeAdd(InstructionStatement instruction, Func<Token, int> resolver, List<byte> output)
    {
        requireOperands(instruction, 2);
        IReadOnlyList<Operand> operands = instruction.operands;

        if (operands[0].kind == OperandKind.IndexRegister)
        {
            writeWord(output, (ushort)(0xF01E | (register(operands[1], "ADD") << 8)));
            return;
        }

        int x = register(operands[0], "ADD");
        if (operands[1].kind == OperandKind.Register)
        {
            writeWord(output, (ushort)(0x8004 | (x << 8) | (operands[1].registerIndex << 4)));
            return;
        }

        writeWord(output, (ushort)(0x7000 | (x << 8) | byte8(immediate(operands[1], resolver), instruction, "ADD")));
    }

    static void encodeLoad(InstructionStatement instruction, Func<Token, int> resolver, List<byte> output)
    {
        requireOperands(instruction, 2);
        Operand destination = instruction.operands[0];
        Operand source = instruction.operands[1];

        switch (destination.kind)
        {
            case OperandKind.Register:
                encodeLoadIntoRegister(instruction, destination.registerIndex, source, resolver, output);
                return;

            case OperandKind.IndexRegister:
                encodeLoadIndex(instruction, source, resolver, output);
                return;

            case OperandKind.DelayTimer:
                writeWord(output, (ushort)(0xF015 | (register(source, "LD DT") << 8)));
                return;

            case OperandKind.SoundTimer:
                writeWord(output, (ushort)(0xF018 | (register(source, "LD ST") << 8)));
                return;

            case OperandKind.FontReg:
                writeWord(output, (ushort)(0xF029 | (register(source, "LD F") << 8)));
                return;

            case OperandKind.BigFontReg:
                writeWord(output, (ushort)(0xF030 | (register(source, "LD HF") << 8)));
                return;

            case OperandKind.BcdReg:
                writeWord(output, (ushort)(0xF033 | (register(source, "LD B") << 8)));
                return;

            case OperandKind.IndexIndirect:
                writeWord(output, (ushort)(0xF055 | (register(source, "LD [I]") << 8)));
                return;

            case OperandKind.RplReg:
                writeWord(output, (ushort)(0xF075 | (register(source, "LD R") << 8)));
                return;

            default:
                throw new AsmException("unsupported LD destination", instruction.position);
        }
    }

    static void encodeLoadIntoRegister(InstructionStatement instruction, int x, Operand source, Func<Token, int> resolver, List<byte> output)
    {
        switch (source.kind)
        {
            case OperandKind.Register:
                writeWord(output, (ushort)(0x8000 | (x << 8) | (source.registerIndex << 4)));
                return;
            case OperandKind.DelayTimer:
                writeWord(output, (ushort)(0xF007 | (x << 8)));
                return;
            case OperandKind.KeyReg:
                writeWord(output, (ushort)(0xF00A | (x << 8)));
                return;
            case OperandKind.IndexIndirect:
                writeWord(output, (ushort)(0xF065 | (x << 8)));
                return;
            case OperandKind.RplReg:
                writeWord(output, (ushort)(0xF085 | (x << 8)));
                return;
            case OperandKind.Immediate:
                writeWord(output, (ushort)(0x6000 | (x << 8) | byte8(immediate(source, resolver), instruction, "LD")));
                return;
            default:
                throw new AsmException("unsupported LD source", instruction.position);
        }
    }

    static void encodeLoadIndex(InstructionStatement instruction, Operand source, Func<Token, int> resolver, List<byte> output)
    {
        if (source.kind != OperandKind.Immediate)
        {
            throw new AsmException("LD I expects an address", instruction.position);
        }

        int value = immediate(source, resolver);
        if (value is < 0 or > 0xFFFF)
        {
            throw new AsmException("index value 0x" + value.ToString("X") + " out of 16-bit range", instruction.position);
        }

        if (value > 0xFFF)
        {
            writeWord(output, 0xF000);
            writeWord(output, (ushort)value);
            return;
        }

        writeWord(output, (ushort)(0xA000 | value));
    }

    static void encodeRange(InstructionStatement instruction, List<byte> output, int op)
    {
        requireOperands(instruction, 1);
        Operand operand = instruction.operands[0];
        if (operand.kind != OperandKind.RegisterRange)
        {
            throw new AsmException(instruction.mnemonic + " expects a 'Vx-Vy' register range", instruction.position);
        }
        writeWord(output, (ushort)(0x5000 | (operand.registerIndex << 8) | (operand.rangeEnd << 4) | op));
    }

    static bool isLongLoad(InstructionStatement instruction, Func<Token, int> resolver)
    {
        if (!string.Equals(instruction.mnemonic, "LD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        IReadOnlyList<Operand> operands = instruction.operands;
        if (operands.Count != 2 || operands[0].kind != OperandKind.IndexRegister || operands[1].kind != OperandKind.Immediate)
        {
            return false;
        }
        return immediate(operands[1], resolver) > 0xFFF;
    }

    static int singleImmediate(InstructionStatement instruction, Func<Token, int> resolver)
    {
        requireOperands(instruction, 1);
        return immediate(instruction.operands[0], resolver);
    }

    static int immediate(Operand operand, Func<Token, int> resolver)
    {
        if (operand.kind != OperandKind.Immediate)
        {
            throw new AsmException("expected a numeric operand but found " + operand, operand.position);
        }
        return Expression.evaluate(operand.expressionTokens, resolver, operand.position);
    }

    static int register(Operand operand, string mnemonic)
    {
        if (operand.kind != OperandKind.Register)
        {
            throw new AsmException(mnemonic + " expects a V register but found " + operand, operand.position);
        }
        return operand.registerIndex;
    }

    static int nibble(int value, InstructionStatement instruction, string mnemonic)
    {
        if (value is < 0 or > 0xF)
        {
            throw new AsmException(mnemonic + " nibble 0x" + value.ToString("X") + " out of range (0-15)", instruction.position);
        }
        return value;
    }

    static int byte8(int value, InstructionStatement instruction, string mnemonic)
    {
        if (value is < 0 or > 0xFF)
        {
            throw new AsmException(mnemonic + " byte 0x" + value.ToString("X") + " out of range (0-255)", instruction.position);
        }
        return value;
    }

    static int address(int value, InstructionStatement instruction, string mnemonic)
    {
        if (value is < 0 or > 0xFFF)
        {
            throw new AsmException(mnemonic + " address 0x" + value.ToString("X") + " out of range (0-0xFFF)", instruction.position);
        }
        return value;
    }

    static void requireOperands(InstructionStatement instruction, int count)
    {
        if (instruction.operands.Count != count)
        {
            throw new AsmException(
                instruction.mnemonic + " expects " + count + " operand(s), got " + instruction.operands.Count,
                instruction.position);
        }
    }

    static void zeroOperands(InstructionStatement instruction)
    {
        if (instruction.operands.Count != 0)
        {
            throw new AsmException(instruction.mnemonic + " takes no operands", instruction.position);
        }
    }

    static void writeWord(List<byte> output, ushort value)
    {
        output.Add((byte)(value >> 8));
        output.Add((byte)(value & 0xFF));
    }
}
