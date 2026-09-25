using System;
using System.Globalization;
using System.IO;
using Chip8.Core;

namespace Chip8.Cli;

static class DisasmCommand
{
    public static int run(string[] args)
    {
        if (args.Length < 1 || CliArgs.isHelp(args))
        {
            Console.WriteLine("usage: chip8 disasm <rom.ch8> [--base 0x200]");
            Console.WriteLine();
            Console.WriteLine("Linear disassembly of every 16-bit word, including SUPER-CHIP and XO-CHIP opcodes");
            Console.WriteLine("(the 4-byte 'i := long' load is decoded as one instruction).");
            Console.WriteLine();
            Console.WriteLine("  --base ADDR       address of the first byte (default 0x200)");
            return args.Length < 1 ? 1 : 0;
        }

        string romPath = args[0];
        ushort baseAddress = Machine.programStart;

        for (int index = 1; index < args.Length; index++)
        {
            if (args[index] == "--base")
            {
                string value = args[++index];
                string digits = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value.Substring(2) : value;
                baseAddress = ushort.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                Console.Error.WriteLine($"error: unknown disasm option {args[index]}");
                return 1;
            }
        }

        if (!File.Exists(romPath))
        {
            Console.Error.WriteLine($"error: ROM not found: {romPath}");
            return 1;
        }

        byte[] rom = File.ReadAllBytes(romPath);
        Console.Write(Disassembler.render(rom, baseAddress));
        return 0;
    }
}
