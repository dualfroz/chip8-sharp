using System;
using System.IO;
using Chip8.Core;

namespace Chip8.Cli;

static class TuiDebugger
{
    public static int run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: chip8 debug <rom.ch8> [--platform NAME]");
            return 1;
        }

        var forwarded = new string[args.Length];
        forwarded[0] = args[0];
        Array.Copy(args, 1, forwarded, 1, args.Length - 1);

        Options options;
        try
        {
            options = Options.parse(forwarded);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }

        if (!File.Exists(options.romPath))
        {
            Console.Error.WriteLine($"error: ROM not found: {options.romPath}");
            return 1;
        }

        var machine = new Machine(options.buildQuirks(), options.seed, options.buildPlatform());
        machine.loadRom(File.ReadAllBytes(options.romPath));

        var console = new DebuggerConsole(new Debugger(machine));
        Console.WriteLine($"chip8-sharp debugger - {Path.GetFileName(options.romPath)} ({options.buildPlatform()})");
        Console.WriteLine("type 'help' for commands");
        Console.Write(console.execute("dis 5"));
        Console.WriteLine();

        while (!console.quitRequested)
        {
            Console.Write("(chip8) ");
            string? line = Console.ReadLine();
            if (line is null)
            {
                break;
            }

            string output = console.execute(line);
            if (output.Length > 0)
            {
                Console.WriteLine(output);
            }
        }

        return 0;
    }
}
