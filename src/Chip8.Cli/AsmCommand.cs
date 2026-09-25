using System;
using System.IO;
using Chip8.Core.Assembly;
using Chip8.OctoAsm;

namespace Chip8.Cli;

static class AsmCommand
{
    const string usage = "usage: chip8 asm <source> [-o out.ch8] [--listing] [--syntax chip8|octo]";

    public static int run(string[] args)
    {
        if (args.Length < 1 || CliArgs.isHelp(args))
        {
            printHelp();
            return args.Length < 1 ? 1 : 0;
        }

        string sourcePath = args[0];
        string? outputPath = null;
        bool showListing = false;
        string? syntax = null;

        for (int index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-o":
                case "--out":
                    outputPath = CliArgs.value(args, ref index);
                    break;
                case "--listing":
                    showListing = true;
                    break;
                case "--syntax":
                    syntax = CliArgs.value(args, ref index).ToLowerInvariant();
                    if (syntax != "chip8" && syntax != "octo")
                    {
                        throw new ArgumentException("--syntax must be 'chip8' or 'octo'");
                    }
                    break;
                default:
                    throw new ArgumentException("unknown asm option " + args[index]);
            }
        }

        if (!File.Exists(sourcePath))
        {
            throw new ArgumentException("source not found: " + sourcePath);
        }

        syntax ??= Path.GetExtension(sourcePath).Equals(".8o", StringComparison.OrdinalIgnoreCase) ? "octo" : "chip8";
        string source = File.ReadAllText(sourcePath);
        byte[] rom;
        string listing;

        try
        {
            if (syntax == "octo")
            {
                OctoProgram program = OctoAssembler.assemble(source);
                rom = program.rom;
                listing = program.renderListing();
            }
            else
            {
                AssemblyResult result = new Assembler().assemble(source);
                rom = result.rom;
                listing = result.listing;
            }
        }
        catch (AsmException error)
        {
            Console.Error.WriteLine($"{sourcePath}: {error.Message}");
            return 1;
        }
        catch (OctoCompileException error)
        {
            foreach (OctoDiagnostic diagnostic in error.diagnostics)
            {
                Console.Error.WriteLine($"{sourcePath}: {diagnostic}");
            }
            return 1;
        }

        outputPath ??= Path.ChangeExtension(sourcePath, ".ch8");
        File.WriteAllBytes(outputPath, rom);
        Console.WriteLine($"assembled {rom.Length} bytes ({syntax} syntax) -> {outputPath}");

        if (showListing)
        {
            Console.Write(listing);
        }

        return 0;
    }

    static void printHelp()
    {
        Console.WriteLine(usage);
        Console.WriteLine();
        Console.WriteLine("Assembles a source file into a .ch8 ROM loaded at 0x200.");
        Console.WriteLine();
        Console.WriteLine("  -o, --out PATH    output file (default: source name with .ch8)");
        Console.WriteLine("  --listing         print an address / bytes / mnemonic listing");
        Console.WriteLine("  --syntax NAME     chip8: classic mnemonics (LD V0, 5) with labels, EQU, DB/DW,");
        Console.WriteLine("                           ORG, ALIGN and MACRO/ENDM");
        Console.WriteLine("                    octo:  Octo source (v0 := 5, if/loop, :macro, :calc, ...)");
        Console.WriteLine("                    default: octo for .8o files, chip8 otherwise");
    }
}
