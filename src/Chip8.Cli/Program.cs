using System;
using System.IO;
using Chip8.Core;
using Chip8.Core.Audio;
using Chip8.Core.Export;
using Chip8.Replay;

namespace Chip8.Cli;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || CliArgs.isHelp(args))
        {
            printUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            switch (args[0])
            {
                case "asm":
                    return AsmCommand.run(args[1..]);
                case "disasm":
                    return DisasmCommand.run(args[1..]);
                case "debug":
                    return TuiDebugger.run(args[1..]);
                case "analyze":
                    return AnalyzeCommand.run(args[1..]);
                case "replay":
                    return ReplayCommand.run(args[1..]);
                case "bench":
                    return BenchCommand.run(args[1..]);
                case "conformance":
                    return ConformanceCommand.run(args[1..]);
            }
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }

        Options options;
        try
        {
            options = Options.parse(args);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine($"error: {error.Message}");
            printUsage();
            return 1;
        }

        if (!File.Exists(options.romPath))
        {
            Console.Error.WriteLine($"error: ROM not found: {options.romPath}");
            return 1;
        }

        if (options.recordPath is not null && options.seed is null)
        {
            options.seed = Random.Shared.Next();
        }

        byte[] rom = File.ReadAllBytes(options.romPath);
        var machine = new Machine(options.buildQuirks(), options.seed, options.buildPlatform());
        machine.loadRom(rom);

        if (options.headless || options.dumpPpmPath is not null || options.dumpWavPath is not null)
        {
            return runHeadless(machine, options);
        }

        InputMovie? recording = options.recordPath is null
            ? null
            : MachineBuilder.headerFor(machine, rom, Path.GetFileName(options.romPath), options.instructionsPerFrame, options.seed);

        RaylibFrontend.run(machine, options, recording);

        if (recording is not null)
        {
            ReplayCommand.writeMovie(recording, options.recordPath!, ReplayCommand.prefersBinary(options.recordPath!));
            Console.WriteLine($"recorded {recording.length} frames ({recording.entryCount} with keys held) -> {options.recordPath}");
        }
        return 0;
    }

    static int runHeadless(Machine machine, Options options)
    {
        AudioTrack? track = null;
        if (options.dumpWavPath is not null)
        {
            int frames = Math.Max(1, options.cycles / options.instructionsPerFrame);
            track = new AudioRecorder().record(machine, frames, options.instructionsPerFrame);
        }
        else
        {
            for (int cycle = 0; cycle < options.cycles; cycle++)
            {
                machine.step();
                if (cycle % options.instructionsPerFrame == options.instructionsPerFrame - 1)
                {
                    machine.tickTimers();
                }
            }
        }

        if (machine.platform == Platform.Chip8)
        {
            Console.Write(AsciiScreen.renderFramed(machine.display));
        }
        else
        {
            Console.Write(AsciiScreen.render(machine.video));
        }

        int litPixels = machine.platform == Platform.Chip8
            ? AsciiScreen.litPixelCount(machine.display)
            : AsciiScreen.litPixelCount(machine.video);
        Console.WriteLine(
            $"rom={Path.GetFileName(options.romPath)} cycles={options.cycles} " +
            $"litPixels={litPixels} soundActive={machine.soundActive}");

        if (options.dumpPpmPath is not null)
        {
            PpmImage.writeFile(options.dumpPpmPath, PpmImage.fromMachine(machine, options.buildPalette(), options.scale));
            Console.WriteLine($"wrote frame to {options.dumpPpmPath}");
        }

        if (options.dumpWavPath is not null && track is not null)
        {
            WavFile.writeFile(options.dumpWavPath, track.toSamples(), AudioRecorder.defaultSampleRate);
            Console.WriteLine($"wrote {track.durationSeconds:0.00}s of audio to {options.dumpWavPath}");
        }

        return 0;
    }

    static void printUsage()
    {
        Console.WriteLine("chip8-sharp - CHIP-8 / SUPER-CHIP / XO-CHIP emulator and toolchain");
        Console.WriteLine();
        Console.WriteLine("usage: chip8 <rom> [options]               run a ROM in a window (or headless)");
        Console.WriteLine("       chip8 asm <source> [options]        assemble CHIP-8 mnemonics or Octo source");
        Console.WriteLine("       chip8 disasm <rom> [--base 0x200]   disassemble a ROM");
        Console.WriteLine("       chip8 debug <rom> [options]         interactive debugger");
        Console.WriteLine("       chip8 analyze <rom> [options]       static and dynamic ROM analysis");
        Console.WriteLine("       chip8 replay <record|play|info> ... record and replay input movies");
        Console.WriteLine("       chip8 bench <rom> [options]         measure emulation speed");
        Console.WriteLine("       chip8 conformance [--roms DIR]      run the bundled test ROM expectations");
        Console.WriteLine();
        Console.WriteLine("run options:");
        Console.WriteLine("  --headless        run without a window, dump the framebuffer as ASCII");
        Console.WriteLine("  --cycles N        instructions to run in headless mode (default 2000)");
        Console.WriteLine("  --ipf N           instructions per 60Hz frame (default 11)");
        Console.WriteLine("  --scale N         window / PPM pixel scale (default 14)");
        Console.WriteLine("  --seed N          deterministic RNG seed");
        Console.WriteLine("  --platform NAME   chip8 (default), superchip, or xochip");
        Console.WriteLine("  --quirks NAME     auto (default), modern, cosmac, superchip, xochip");
        Console.WriteLine("  --palette NAME    octo (default), mono, grayscale, extended16");
        Console.WriteLine("  --ppm PATH        export the final frame as a PPM image");
        Console.WriteLine("  --wav PATH        export the buzzer audio as a WAV file");
        Console.WriteLine("  --frames N        window mode: close after N frames");
        Console.WriteLine("  --shot PATH       window mode: save a PNG screenshot before closing");
        Console.WriteLine("  --record PATH     window mode: record keyboard input to a movie file");
        Console.WriteLine();
        Console.WriteLine("run 'chip8 <command> --help' for the options of a command");
        Console.WriteLine("keys: 1234/QWER/ASDF/ZXCV map to the CHIP-8 hex pad");
    }
}
