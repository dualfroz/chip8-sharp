using System.Collections.Generic;
using Chip8.Core;

namespace Chip8.Analysis;

public static class InstructionDecoder
{
    public const ushort defaultBaseAddress = 0x200;

    public static InstructionInfo? decodeAt(byte[] program, ushort address, ushort baseAddress = defaultBaseAddress)
    {
        int offset = address - baseAddress;
        if (offset < 0 || offset + 1 >= program.Length)
        {
            return null;
        }

        ushort opcode = (ushort)((program[offset] << 8) | program[offset + 1]);

        if (opcode == 0xF000 && offset + 3 < program.Length)
        {
            ushort extraWord = (ushort)((program[offset + 2] << 8) | program[offset + 3]);
            return decode(address, opcode, extraWord, 4);
        }

        return decode(address, opcode, null, 2);
    }

    public static int byteLengthAt(byte[] program, ushort address, ushort baseAddress = defaultBaseAddress)
    {
        int offset = address - baseAddress;
        if (offset < 0 || offset + 1 >= program.Length)
        {
            return 2;
        }
        ushort opcode = (ushort)((program[offset] << 8) | program[offset + 1]);
        return opcode == 0xF000 && offset + 3 < program.Length ? 4 : 2;
    }

    public static InstructionInfo decode(ushort address, ushort opcode, ushort? extraWord, int byteLength)
    {
        var (kind, category, flow) = classify(opcode);
        string mnemonic = Disassembler.describe(opcode, extraWord ?? 0);
        return new InstructionInfo(address, opcode, extraWord, byteLength, kind, category, flow, mnemonic);
    }

    public static IReadOnlyList<InstructionInfo> decodeLinear(byte[] program, ushort baseAddress = defaultBaseAddress)
    {
        var instructions = new List<InstructionInfo>();
        int offset = 0;
        while (offset + 1 < program.Length)
        {
            ushort address = (ushort)(baseAddress + offset);
            var info = decodeAt(program, address, baseAddress);
            if (info is null)
            {
                break;
            }
            instructions.Add(info);
            offset += info.byteLength;
        }
        return instructions;
    }

    static (InstructionKind, InstructionCategory, FlowEffect) classify(ushort opcode)
    {
        int high = opcode & 0xF000;
        int x = (opcode & 0x0F00) >> 8;
        int y = (opcode & 0x00F0) >> 4;
        int n = opcode & 0x000F;
        int nn = opcode & 0x00FF;

        switch (high)
        {
            case 0x0000:
                return classifySystem(opcode, n);

            case 0x1000:
                return (InstructionKind.Jump, InstructionCategory.Control, FlowEffect.Jump);

            case 0x2000:
                return (InstructionKind.Call, InstructionCategory.Control, FlowEffect.Call);

            case 0x3000:
                return (InstructionKind.SkipEqualImmediate, InstructionCategory.Skip, FlowEffect.Branch);

            case 0x4000:
                return (InstructionKind.SkipNotEqualImmediate, InstructionCategory.Skip, FlowEffect.Branch);

            case 0x5000:
                return n switch
                {
                    0x0 => (InstructionKind.SkipEqualRegister, InstructionCategory.Skip, FlowEffect.Branch),
                    0x2 => (InstructionKind.SaveRange, InstructionCategory.Memory, FlowEffect.Fallthrough),
                    0x3 => (InstructionKind.LoadRange, InstructionCategory.Memory, FlowEffect.Fallthrough),
                    _ => dataWord,
                };

            case 0x6000:
                return (InstructionKind.LoadImmediate, InstructionCategory.Arithmetic, FlowEffect.Fallthrough);

            case 0x7000:
                return (InstructionKind.AddImmediate, InstructionCategory.Arithmetic, FlowEffect.Fallthrough);

            case 0x8000:
                return classifyArithmetic(n);

            case 0x9000:
                return n == 0
                    ? (InstructionKind.SkipNotEqualRegister, InstructionCategory.Skip, FlowEffect.Branch)
                    : dataWord;

            case 0xA000:
                return (InstructionKind.LoadIndex, InstructionCategory.Memory, FlowEffect.Fallthrough);

            case 0xB000:
                return (InstructionKind.JumpWithOffset, InstructionCategory.Control, FlowEffect.IndirectJump);

            case 0xC000:
                return (InstructionKind.Random, InstructionCategory.Arithmetic, FlowEffect.Fallthrough);

            case 0xD000:
                return (InstructionKind.Draw, InstructionCategory.Draw, FlowEffect.Fallthrough);

            case 0xE000:
                return nn switch
                {
                    0x9E => (InstructionKind.SkipKeyPressed, InstructionCategory.Skip, FlowEffect.Branch),
                    0xA1 => (InstructionKind.SkipKeyReleased, InstructionCategory.Skip, FlowEffect.Branch),
                    _ => dataWord,
                };

            case 0xF000:
                return classifyMisc(opcode, nn);
        }

        return dataWord;
    }

    static (InstructionKind, InstructionCategory, FlowEffect) classifySystem(ushort opcode, int n)
    {
        if ((opcode & 0xFFF0) == 0x00C0)
        {
            return (InstructionKind.ScrollDown, InstructionCategory.Draw, FlowEffect.Fallthrough);
        }
        if ((opcode & 0xFFF0) == 0x00D0)
        {
            return (InstructionKind.ScrollUp, InstructionCategory.Draw, FlowEffect.Fallthrough);
        }

        return opcode switch
        {
            0x00E0 => (InstructionKind.ClearScreen, InstructionCategory.Draw, FlowEffect.Fallthrough),
            0x00EE => (InstructionKind.Return, InstructionCategory.Control, FlowEffect.Return),
            0x00FB => (InstructionKind.ScrollRight, InstructionCategory.Draw, FlowEffect.Fallthrough),
            0x00FC => (InstructionKind.ScrollLeft, InstructionCategory.Draw, FlowEffect.Fallthrough),
            0x00FD => (InstructionKind.Exit, InstructionCategory.Control, FlowEffect.Terminate),
            0x00FE => (InstructionKind.LowRes, InstructionCategory.Draw, FlowEffect.Fallthrough),
            0x00FF => (InstructionKind.HiRes, InstructionCategory.Draw, FlowEffect.Fallthrough),
            _ => (InstructionKind.System, InstructionCategory.Control, FlowEffect.Fallthrough),
        };
    }

    static (InstructionKind, InstructionCategory, FlowEffect) classifyArithmetic(int n) => n switch
    {
        0x0 or 0x1 or 0x2 or 0x3 or 0x4 or 0x5 or 0x6 or 0x7 or 0xE =>
            (InstructionKind.Arithmetic, InstructionCategory.Arithmetic, FlowEffect.Fallthrough),
        _ => dataWord,
    };

    static (InstructionKind, InstructionCategory, FlowEffect) classifyMisc(ushort opcode, int nn)
    {
        if (opcode == 0xF000)
        {
            return (InstructionKind.LoadLong, InstructionCategory.Memory, FlowEffect.Fallthrough);
        }
        if ((opcode & 0xF0FF) == 0xF001)
        {
            return (InstructionKind.Plane, InstructionCategory.Draw, FlowEffect.Fallthrough);
        }
        if (opcode == 0xF002)
        {
            return (InstructionKind.Audio, InstructionCategory.Sound, FlowEffect.Fallthrough);
        }

        return nn switch
        {
            0x07 => (InstructionKind.LoadDelay, InstructionCategory.Timer, FlowEffect.Fallthrough),
            0x0A => (InstructionKind.WaitKey, InstructionCategory.Io, FlowEffect.Fallthrough),
            0x15 => (InstructionKind.SetDelay, InstructionCategory.Timer, FlowEffect.Fallthrough),
            0x18 => (InstructionKind.SetSound, InstructionCategory.Sound, FlowEffect.Fallthrough),
            0x1E => (InstructionKind.AddIndex, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x29 => (InstructionKind.LoadFont, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x30 => (InstructionKind.LoadBigFont, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x33 => (InstructionKind.StoreBcd, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x3A => (InstructionKind.Pitch, InstructionCategory.Sound, FlowEffect.Fallthrough),
            0x55 => (InstructionKind.StoreRegisters, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x65 => (InstructionKind.LoadRegisters, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x75 => (InstructionKind.StoreFlags, InstructionCategory.Memory, FlowEffect.Fallthrough),
            0x85 => (InstructionKind.LoadFlags, InstructionCategory.Memory, FlowEffect.Fallthrough),
            _ => dataWord,
        };
    }

    static (InstructionKind, InstructionCategory, FlowEffect) dataWord =>
        (InstructionKind.Data, InstructionCategory.Data, FlowEffect.Fallthrough);
}
