using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chip8.Core.Assembly;

public sealed class Assembler
{
    const int layoutIterationLimit = 32;

    readonly ushort baseAddress;

    public Assembler(ushort baseAddress = Machine.programStart)
    {
        this.baseAddress = baseAddress;
    }

    public static byte[] assembleRom(string source, ushort baseAddress = Machine.programStart) =>
        new Assembler(baseAddress).assemble(source).rom;

    public AssemblyResult assemble(string source)
    {
        List<Token> tokens = new Lexer(source).tokenize();
        List<Token> expanded = new MacroExpander().expand(tokens);
        List<Statement> statements = new Parser(expanded).parse();

        Dictionary<string, int> symbols = resolveSymbols(statements);
        return emit(statements, symbols);
    }

    Dictionary<string, int> resolveSymbols(List<Statement> statements)
    {
        var symbols = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int iteration = 0; iteration < layoutIterationLimit; iteration++)
        {
            var next = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<Token, int> resolver = token => symbols.TryGetValue(token.text, out int value) ? value : 0;
            int address = baseAddress;

            foreach (Statement statement in statements)
            {
                address = layoutStatement(statement, next, resolver, address);
            }

            if (sameSymbols(symbols, next))
            {
                return next;
            }

            symbols = next;
        }

        throw new AsmException("symbol layout did not converge (possible circular reference)", new SourcePosition(0, 0));
    }

    int layoutStatement(Statement statement, Dictionary<string, int> symbols, Func<Token, int> resolver, int address)
    {
        switch (statement)
        {
            case LabelStatement label:
                define(symbols, label.name, address, label.position);
                return address;

            case ConstantStatement constant:
                define(symbols, constant.name, Expression.evaluate(constant.expression, resolver, constant.position), constant.position);
                return address;

            case OrgStatement org:
                return requireForward(Expression.evaluate(org.expression, resolver, org.position), org.position, address);

            case AlignStatement align:
                return alignTo(address, Expression.evaluate(align.expression, resolver, align.position), align.position);

            case DataStatement data:
                return address + data.items.Count * (data.word ? 2 : 1);

            case InstructionStatement instruction:
                return address + InstructionEncoder.measure(instruction, resolver);

            default:
                return address;
        }
    }

    AssemblyResult emit(List<Statement> statements, Dictionary<string, int> symbols)
    {
        Func<Token, int> resolver = token => symbols.TryGetValue(token.text, out int value)
            ? value
            : throw new AsmException("undefined symbol '" + token.text + "'", token.position);

        var writes = new Dictionary<int, byte>();
        var listing = new StringBuilder();
        int address = baseAddress;
        int highest = baseAddress;

        foreach (Statement statement in statements)
        {
            switch (statement)
            {
                case LabelStatement label:
                    listing.Append(labelLine(label.name, address));
                    break;

                case ConstantStatement constant:
                    listing.Append(constantLine(constant.name, symbols[constant.name]));
                    break;

                case OrgStatement org:
                    address = requireForward(Expression.evaluate(org.expression, resolver, org.position), org.position, address);
                    break;

                case AlignStatement align:
                    address = alignTo(address, Expression.evaluate(align.expression, resolver, align.position), align.position);
                    break;

                case DataStatement data:
                    address = emitData(data, resolver, writes, listing, address);
                    break;

                case InstructionStatement instruction:
                    address = emitInstruction(instruction, resolver, writes, listing, address);
                    break;
            }

            highest = Math.Max(highest, address);
        }

        return new AssemblyResult(buildRom(writes, highest), baseAddress, symbols, listing.ToString());
    }

    int emitData(DataStatement data, Func<Token, int> resolver, Dictionary<int, byte> writes, StringBuilder listing, int address)
    {
        int start = address;
        var bytes = new List<byte>();

        foreach (IReadOnlyList<Token> item in data.items)
        {
            int value = Expression.evaluate(item, resolver, data.position);
            if (data.word)
            {
                if (value is < 0 or > 0xFFFF)
                {
                    throw new AsmException("DW value 0x" + value.ToString("X") + " out of 16-bit range", data.position);
                }
                bytes.Add((byte)(value >> 8));
                bytes.Add((byte)(value & 0xFF));
            }
            else
            {
                if (value is < -128 or > 0xFF)
                {
                    throw new AsmException("DB value 0x" + value.ToString("X") + " out of byte range", data.position);
                }
                bytes.Add((byte)(value & 0xFF));
            }
        }

        foreach (byte value in bytes)
        {
            writes[address++] = value;
        }

        listing.Append(bytesLine(start, bytes, data.word ? "DW" : "DB"));
        return address;
    }

    int emitInstruction(InstructionStatement instruction, Func<Token, int> resolver, Dictionary<int, byte> writes, StringBuilder listing, int address)
    {
        var bytes = new List<byte>();
        InstructionEncoder.emit(instruction, resolver, bytes);

        int start = address;
        foreach (byte value in bytes)
        {
            writes[address++] = value;
        }

        ushort opcode = (ushort)((bytes[0] << 8) | bytes[1]);
        ushort extra = bytes.Count == 4 ? (ushort)((bytes[2] << 8) | bytes[3]) : (ushort)0;
        listing.Append(instructionLine(start, bytes, Disassembler.describe(opcode, extra)));
        return address;
    }

    byte[] buildRom(Dictionary<int, byte> writes, int highest)
    {
        int length = Math.Max(0, highest - baseAddress);
        var rom = new byte[length];
        foreach (KeyValuePair<int, byte> write in writes)
        {
            rom[write.Key - baseAddress] = write.Value;
        }
        return rom;
    }

    static void define(Dictionary<string, int> symbols, string name, int value, SourcePosition position)
    {
        if (symbols.ContainsKey(name))
        {
            throw new AsmException("duplicate definition of '" + name + "'", position);
        }
        symbols[name] = value;
    }

    int requireForward(int target, SourcePosition position, int address)
    {
        if (target < baseAddress)
        {
            throw new AsmException("ORG 0x" + target.ToString("X") + " is below base 0x" + baseAddress.ToString("X"), position);
        }
        return target;
    }

    static int alignTo(int address, int boundary, SourcePosition position)
    {
        if (boundary <= 0)
        {
            throw new AsmException("ALIGN needs a positive boundary", position);
        }
        int remainder = address % boundary;
        return remainder == 0 ? address : address + (boundary - remainder);
    }

    static bool sameSymbols(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        foreach (KeyValuePair<string, int> entry in left)
        {
            if (!right.TryGetValue(entry.Key, out int value) || value != entry.Value)
            {
                return false;
            }
        }
        return true;
    }

    static string labelLine(string name, int address) =>
        "                   " + name + ": (= 0x" + address.ToString("X4", CultureInfo.InvariantCulture) + ")\n";

    static string constantLine(string name, int value) =>
        "                   " + name + " = 0x" + value.ToString("X", CultureInfo.InvariantCulture) + "\n";

    static string instructionLine(int address, List<byte> bytes, string text) =>
        addressColumn(address) + hexColumn(bytes).PadRight(12) + text + "\n";

    static string bytesLine(int address, List<byte> bytes, string keyword) =>
        addressColumn(address) + hexColumn(bytes).PadRight(12) + keyword + "\n";

    static string addressColumn(int address) =>
        "0x" + address.ToString("X4", CultureInfo.InvariantCulture) + ":  ";

    static string hexColumn(List<byte> bytes)
    {
        var builder = new StringBuilder();
        foreach (byte value in bytes)
        {
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
        }
        return builder.ToString().TrimEnd();
    }
}
