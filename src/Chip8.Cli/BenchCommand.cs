using System;
using System.Diagnostics;
using System.IO;
using Chip8.Core;
using Chip8.Replay;

namespace Chip8.Cli;

static class BenchCommand
{
    public static int run(string[] args)
    {
        if (args.Length < 1 || CliArgs.isHelp(args))
        {
            printHelp();
            return args.Length < 1 ? 1 : 0;
        }

        string romPath = args[0];
        int frames = 60_000;
        int instructionsPerFrame = 11;
        string platformName = "chip8";
        string quirksName = "auto";
        int seed = 1;

        for (int index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--frames":
                    frames = CliArgs.positive(args, ref index);
                    break;
                case "--ipf":
                    instructionsPerFrame = CliArgs.positive(args, ref index);
                    break;
                case "--platform":
                    platformName = CliArgs.value(args, ref index);
                    break;
                case "--quirks":
                    quirksName = CliArgs.value(args, ref index);
                    break;
                case "--seed":
                    seed = CliArgs.integer(args, ref index);
                    break;
                default:
                    throw new ArgumentException("unknown bench option " + args[index]);
            }
        }

        byte[] rom = CliArgs.readRom(romPath);
        var machine = new Machine(Options.quirksFrom(quirksName), seed, Options.platformFrom(platformName));
        machine.loadRom(rom);

        int steps = 0;
        int framesRun = 0;
        var clock = Stopwatch.StartNew();
        while (framesRun < frames && !machine.exited)
        {
            for (int cycle = 0; cycle < instructionsPerFrame && !machine.exited; cycle++)
            {
                machine.step();
                steps++;
            }
            machine.tickTimers();
            framesRun++;
        }
        clock.Stop();

        var benchmark = new BenchmarkResult(
            Path.GetFileName(romPath),
            steps,
            framesRun,
            clock.Elapsed.TotalMilliseconds,
            FramebufferHash.compute(machine),
            machine.exited);

        Console.WriteLine(benchmark.format());
        Console.WriteLine("framebuffer hash: " + FramebufferHash.format(benchmark.framebufferHash) + (benchmark.exited ? " (exited)" : ""));
        return 0;
    }

    static void printHelp()
    {
        Console.WriteLine("usage: chip8 bench <rom> [options]");
        Console.WriteLine();
        Console.WriteLine("Runs the ROM headlessly as fast as possible and reports instructions and frames per second.");
        Console.WriteLine("The framebuffer hash at the end makes runs comparable across builds.");
        Console.WriteLine();
        Console.WriteLine("  --frames N        60 Hz frames to emulate (default 60000)");
        Console.WriteLine("  --ipf N           instructions per frame (default 11)");
        Console.WriteLine("  --platform NAME   chip8 (default), superchip, or xochip");
        Console.WriteLine("  --quirks NAME     auto (default), modern, cosmac, superchip, xochip");
        Console.WriteLine("  --seed N          RNG seed (default 1)");
    }
}
