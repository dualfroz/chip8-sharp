using System;
using System.IO;
using Chip8.Core;
using Chip8.Core.Assembly;

namespace Chip8.Cli;

static class AsmCommand
{
    public static int run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: chip8 asm <source.asm> [-o out.ch8] [--listing]");
            return 1;
        }

        string sourcePath = args[0];
        string? outputPath = null;
        bool showListing = false;

        for (int index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-o":
                case "--out":
                    outputPath = args[++index];
                    break;
                case "--listing":
                    showListing = true;
                    break;
                default:
                    Console.Error.WriteLine($"error: unknown asm option {args[index]}");
                    return 1;
            }
        }

        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"error: source not found: {sourcePath}");
            return 1;
        }

        string source = File.ReadAllText(sourcePath);
        AssemblyResult result;
        try
        {
            result = new Assembler().assemble(source);
        }
        catch (AsmException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }

        outputPath ??= Path.ChangeExtension(sourcePath, ".ch8");
        File.WriteAllBytes(outputPath, result.rom);
        Console.WriteLine($"assembled {result.rom.Length} bytes -> {outputPath}");

        if (showListing)
        {
            Console.Write(result.listing);
        }

        return 0;
    }
}
