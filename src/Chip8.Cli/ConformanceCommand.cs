using System;
using System.Collections.Generic;
using System.IO;
using Chip8.Replay;

namespace Chip8.Cli;

static class ConformanceCommand
{
    public static int run(string[] args)
    {
        if (CliArgs.isHelp(args))
        {
            printHelp();
            return 0;
        }

        string? romDirectory = null;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "--roms")
            {
                romDirectory = CliArgs.value(args, ref index);
            }
            else
            {
                throw new ArgumentException("unknown conformance option " + args[index]);
            }
        }

        RomLibrary library;
        if (romDirectory is null)
        {
            try
            {
                library = RomLibrary.locate();
            }
            catch (DirectoryNotFoundException error)
            {
                throw new ArgumentException(error.Message + "; pass --roms DIR");
            }
        }
        else
        {
            library = new RomLibrary(romDirectory);
        }

        var runner = new ConformanceRunner(library);
        int failures = 0;
        int executed = 0;
        foreach (RomExpectation expectation in ConformanceRunner.builtInExpectations())
        {
            if (!library.has(expectation.romName))
            {
                Console.WriteLine("SKIP " + expectation.romName + " (not found in " + library.directory + ")");
                continue;
            }

            TestRomResult outcome = runner.run(expectation);
            Console.WriteLine(outcome.format());
            executed++;
            if (!outcome.passed)
            {
                failures++;
            }
        }

        if (executed == 0)
        {
            Console.Error.WriteLine("error: none of the test ROMs were found in " + library.directory);
            return 1;
        }

        Console.WriteLine($"{executed - failures} of {executed} passed");
        return failures == 0 ? 0 : 1;
    }

    static void printHelp()
    {
        Console.WriteLine("usage: chip8 conformance [--roms DIR]");
        Console.WriteLine();
        Console.WriteLine("Runs the built-in expectations for the community test ROMs (IBM logo, CHIP-8 logo,");
        Console.WriteLine("corax+, flags, corax89 opcode test) and checks the lit pixel count of the final frame.");
        Console.WriteLine("Exits with status 1 if any ROM fails.");
        Console.WriteLine();
        Console.WriteLine("  --roms DIR        directory with the .ch8 files (default: the nearest roms/ or");
        Console.WriteLine("                    tests/roms/ directory above the executable)");
    }
}
