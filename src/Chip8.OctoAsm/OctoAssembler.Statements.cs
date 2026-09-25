using System;

namespace Chip8.OctoAsm;

public sealed partial class OctoAssembler
{
    bool instructionStatement(OctoToken token)
    {
        SourcePosition where = token.position;
        switch (token.text)
        {
            case "return":
            case ";":
                emitInstruction(0x00EE, where);
                return true;
            case "clear":
                emitInstruction(0x00E0, where);
                return true;
            case "hires":
                emitInstruction(0x00FF, where);
                return true;
            case "lores":
                emitInstruction(0x00FE, where);
                return true;
            case "exit":
                emitInstruction(0x00FD, where);
                return true;
            case "scroll-left":
                emitInstruction(0x00FC, where);
                return true;
            case "scroll-right":
                emitInstruction(0x00FB, where);
                return true;
            case "audio":
                emitInstruction(0xF002, where);
                return true;
            case "scroll-down":
                emitInstruction(0x00C0 | nibbleOperand(next()), where);
                return true;
            case "scroll-up":
                emitInstruction(0x00D0 | nibbleOperand(next()), where);
                return true;
            case "plane":
            {
                int mask = nibbleOperand(next());
                if (mask > 3)
                {
                    throw error("plane mask must be 0..3", where);
                }
                emitInstruction(0xF001 | (mask << 8), where);
                return true;
            }
            case "bcd":
                emitInstruction(0xF033 | (registerIndex(next()) << 8), where);
                return true;
            case "saveflags":
                emitInstruction(0xF075 | (registerIndex(next()) << 8), where);
                return true;
            case "loadflags":
                emitInstruction(0xF085 | (registerIndex(next()) << 8), where);
                return true;
            case "save":
                emitRegisterTransfer(0xF055, 0x5002, where);
                return true;
            case "load":
                emitRegisterTransfer(0xF065, 0x5003, where);
                return true;
            case "sprite":
            {
                int x = registerIndex(next());
                int y = registerIndex(next());
                int rows = nibbleOperand(next());
                emitInstruction(0xD000 | (x << 8) | (y << 4) | rows, where);
                return true;
            }
            case "jump":
                emitAddressInstruction(0x1000, next());
                return true;
            case "jump0":
                emitAddressInstruction(0xB000, next());
                return true;
            case "native":
                emitAddressInstruction(0x0000, next());
                return true;
            case "delay":
                expectWord(":=");
                emitInstruction(0xF015 | (registerIndex(next()) << 8), where);
                return true;
            case "buzzer":
                expectWord(":=");
                emitInstruction(0xF018 | (registerIndex(next()) << 8), where);
                return true;
            case "pitch":
                expectWord(":=");
                emitInstruction(0xF03A | (registerIndex(next()) << 8), where);
                return true;
            case "i":
                indexStatement(where);
                return true;
            default:
                return false;
        }
    }

    void emitRegisterTransfer(int singleOpcode, int rangeOpcode, SourcePosition where)
    {
        int x = registerIndex(next());
        if (!peek().isWord("-"))
        {
            emitInstruction(singleOpcode | (x << 8), where);
            return;
        }

        next();
        int y = registerIndex(next());
        emitInstruction(rangeOpcode | (x << 8) | (y << 4), where);
    }

    void indexStatement(SourcePosition where)
    {
        OctoToken op = next();
        if (op.isWord("+="))
        {
            emitInstruction(0xF01E | (registerIndex(next()) << 8), where);
            return;
        }

        if (!op.isWord(":="))
        {
            throw error("expected ':=' or '+=' after 'i' but found " + op, op.position);
        }

        OctoToken source = next();
        if (source.isWord("hex"))
        {
            emitInstruction(0xF029 | (registerIndex(next()) << 8), where);
            return;
        }
        if (source.isWord("bighex"))
        {
            emitInstruction(0xF030 | (registerIndex(next()) << 8), where);
            return;
        }
        if (source.isWord("long"))
        {
            ensureEntry(where);
            int value = addressOperand(next(), FixupKind.Word16, image.here + 2);
            emitInstruction(0xF000, where);
            emitWord(value, where);
            return;
        }

        emitAddressInstruction(0xA000, source);
    }

    void registerStatement(OctoToken target)
    {
        int x = registerIndex(target);
        OctoToken op = next();
        SourcePosition where = target.position;

        switch (op.text)
        {
            case ":=":
            {
                OctoToken source = next();
                if (isRegister(source))
                {
                    emitInstruction(0x8000 | (x << 8) | (registerIndex(source) << 4), where);
                }
                else if (source.isWord("random"))
                {
                    emitInstruction(0xC000 | (x << 8) | byteOperand(next()), where);
                }
                else if (source.isWord("key"))
                {
                    emitInstruction(0xF00A | (x << 8), where);
                }
                else if (source.isWord("delay"))
                {
                    emitInstruction(0xF007 | (x << 8), where);
                }
                else
                {
                    emitInstruction(0x6000 | (x << 8) | byteOperand(source), where);
                }
                return;
            }

            case "+=":
            {
                OctoToken source = next();
                emitInstruction(isRegister(source)
                    ? 0x8004 | (x << 8) | (registerIndex(source) << 4)
                    : 0x7000 | (x << 8) | byteOperand(source), where);
                return;
            }

            case "-=":
            {
                OctoToken source = next();
                emitInstruction(isRegister(source)
                    ? 0x8005 | (x << 8) | (registerIndex(source) << 4)
                    : 0x7000 | (x << 8) | ((0x100 - byteOperand(source)) & 0xFF), where);
                return;
            }

            case "=-":
                emitInstruction(0x8007 | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            case "|=":
                emitInstruction(0x8001 | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            case "&=":
                emitInstruction(0x8002 | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            case "^=":
                emitInstruction(0x8003 | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            case ">>=":
                emitInstruction(0x8006 | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            case "<<=":
                emitInstruction(0x800E | (x << 8) | (registerIndex(next()) << 4), where);
                return;
            default:
                throw error("unknown register operator " + op, op.position);
        }
    }

    void emitByte(int value, SourcePosition where)
    {
        ensureEntry(where);
        image.emit(value, where);
    }

    void emitWord(int value, SourcePosition where)
    {
        ensureEntry(where);
        image.emitWord(value, where);
    }

    void emitInstruction(int opcode, SourcePosition where)
    {
        ensureEntry(where);
        instructionAddresses.Add(image.here);
        image.emitWord(opcode, where);
    }

    void emitAddressInstruction(int opcode, OctoToken target)
    {
        ensureEntry(target.position);
        int address = addressOperand(target, FixupKind.Address12, image.here);
        emitInstruction(opcode | checkAddress12(address, target.text, target.position), target.position);
    }

    int addressOperand(OctoToken token, FixupKind kind, int fixupAddress)
    {
        if (tryResolveAddress(token, out int value))
        {
            if (value < 0 || value >= OctoImage.addressLimit)
            {
                throw error("address " + value + " is outside 0..0xFFFF", token.position);
            }
            return value;
        }

        requireForwardName(token);
        fixups.Add(new Fixup(token.text, fixupAddress, kind, token.position));
        return 0;
    }

    bool tryResolveAddress(OctoToken token, out int value)
    {
        value = 0;
        if (token.kind == OctoTokenKind.Number)
        {
            value = token.value;
            return true;
        }
        if (token.kind != OctoTokenKind.Word)
        {
            return false;
        }
        if (labels.TryGetValue(token.text, out int address))
        {
            value = address;
            return true;
        }
        if (constants.TryGetValue(token.text, out double constant))
        {
            value = (int)constant;
            return true;
        }
        return false;
    }

    void requireForwardName(OctoToken token)
    {
        if (token.kind != OctoTokenKind.Word || keywords.Contains(token.text) || isRegister(token))
        {
            throw error("expected an address or label but found " + token, token.position);
        }
    }

    int byteOperand(OctoToken token)
    {
        if (token.kind == OctoTokenKind.Number)
        {
            return checkByte(token.value, token);
        }
        if (token.kind == OctoTokenKind.Word && constants.TryGetValue(token.text, out double value))
        {
            return checkByte((int)value, token);
        }
        throw error("expected a number or constant but found " + token, token.position);
    }

    static int checkByte(int value, OctoToken token)
    {
        if (value < -128 || value > 0xFF)
        {
            throw error("value " + value + " does not fit in a byte", token.position);
        }
        return value & 0xFF;
    }

    int nibbleOperand(OctoToken token)
    {
        int value = byteOperand(token);
        if (value > 0xF)
        {
            throw error("value " + value + " does not fit in a nibble", token.position);
        }
        return value;
    }

    double constantValue(OctoToken token)
    {
        if (token.kind == OctoTokenKind.Number)
        {
            return token.value;
        }
        if (token.kind == OctoTokenKind.Word && constants.TryGetValue(token.text, out double value))
        {
            return value;
        }
        throw error("expected a number or constant but found " + token, token.position);
    }

    static int checkAddress12(int address, string name, SourcePosition where)
    {
        if (address > 0xFFF)
        {
            throw error("'" + name + "' (0x" + address.ToString("X") + ") is beyond the 12-bit address range; use 'i := long'", where);
        }
        return address;
    }

    bool isRegister(OctoToken token) =>
        token.kind == OctoTokenKind.Word && (parseRegister(token.text) >= 0 || aliases.ContainsKey(token.text));

    int registerIndex(OctoToken token)
    {
        if (token.kind == OctoTokenKind.Word)
        {
            int index = parseRegister(token.text);
            if (index >= 0)
            {
                return index;
            }
            if (aliases.TryGetValue(token.text, out int aliased))
            {
                return aliased;
            }
        }
        throw error("expected a register but found " + token, token.position);
    }

    static int parseRegister(string text)
    {
        if (text.Length != 2 || (text[0] != 'v' && text[0] != 'V'))
        {
            return -1;
        }
        return Uri.IsHexDigit(text[1]) ? Convert.ToInt32(text.Substring(1), 16) : -1;
    }
}
