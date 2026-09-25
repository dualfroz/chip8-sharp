using System;
using System.IO;
using System.Text;
using Chip8.Core;
using Chip8.Core.Export;
using Chip8.Replay;

namespace Chip8.Cli;

static class ReplayCommand
{
    public static int run(string[] args)
    {
        if (args.Length < 1 || CliArgs.isHelp(args))
        {
            printHelp();
            return args.Length < 1 ? 1 : 0;
        }

        return args[0] switch
        {
            "record" => record(args[1..]),
            "play" => play(args[1..]),
            "info" => info(args[1..]),
            _ => throw new ArgumentException("unknown replay command '" + args[0] + "' (record, play or info)"),
        };
    }

    static int record(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("usage: chip8 replay record <rom> <script> -o <movie> [options]");
        }

        string romPath = args[0];
        string scriptPath = args[1];
        string? outputPath = null;
        string platformName = "chip8";
        string quirksName = "auto";
        int seed = 1;
        int instructionsPerFrame = 11;
        bool binary = false;

        for (int index = 2; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "-o":
                case "--out":
                    outputPath = CliArgs.value(args, ref index);
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
                case "--ipf":
                    instructionsPerFrame = CliArgs.positive(args, ref index);
                    break;
                case "--binary":
                    binary = true;
                    break;
                default:
                    throw new ArgumentException("unknown replay record option " + args[index]);
            }
        }

        if (outputPath is null)
        {
            throw new ArgumentException("replay record needs an output movie: -o <movie>");
        }
        if (!File.Exists(scriptPath))
        {
            throw new ArgumentException("script not found: " + scriptPath);
        }

        byte[] rom = CliArgs.readRom(romPath);
        var machine = new Machine(Options.quirksFrom(quirksName), seed, Options.platformFrom(platformName));
        machine.loadRom(rom);
        InputMovie movie = MachineBuilder.headerFor(machine, rom, Path.GetFileName(romPath), instructionsPerFrame, seed);
        var runner = new ScriptRunner(machine, instructionsPerFrame, movie);

        try
        {
            runner.run(InputScript.parse(File.ReadAllText(scriptPath)));
        }
        catch (ScriptException error)
        {
            Console.Error.WriteLine($"{scriptPath}: {error.Message}");
            return 1;
        }

        writeMovie(movie, outputPath, binary || prefersBinary(outputPath));
        Console.WriteLine($"recorded {movie.length} frames ({movie.entryCount} with keys held) -> {outputPath}");
        Console.WriteLine($"assertions passed: {runner.assertionsPassed}");
        Console.WriteLine(ReplayOutcome.capture(machine, runner.currentFrame).ToString());
        return 0;
    }

    static int play(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("usage: chip8 replay play <movie> <rom> [options]");
        }

        string moviePath = args[0];
        string romPath = args[1];
        bool verify = false;
        bool screen = false;
        bool window = false;
        bool force = false;
        string? ppmPath = null;
        string paletteName = "octo";
        int scale = 14;

        for (int index = 2; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--verify":
                    verify = true;
                    break;
                case "--screen":
                    screen = true;
                    break;
                case "--window":
                    window = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--ppm":
                    ppmPath = CliArgs.value(args, ref index);
                    break;
                case "--palette":
                    paletteName = CliArgs.value(args, ref index);
                    break;
                case "--scale":
                    scale = CliArgs.positive(args, ref index);
                    break;
                default:
                    throw new ArgumentException("unknown replay play option " + args[index]);
            }
        }

        InputMovie movie = readMovie(moviePath);
        byte[] rom = CliArgs.readRom(romPath);
        if (!movie.matchesRom(rom) && !force)
        {
            Console.Error.WriteLine(
                $"error: {Path.GetFileName(romPath)} has hash {RomHash.toHex(rom)} but the movie was recorded on " +
                $"{movie.romName} ({RomHash.format(movie.romHash)}); use --force to play anyway");
            return 1;
        }

        if (window)
        {
            var options = new Options { romPath = romPath, scale = scale, paletteName = paletteName };
            RaylibFrontend.run(MachineBuilder.fromMovie(movie, rom), options, null, movie);
            return 0;
        }

        ReplayEngine engine = ReplayEngine.playback(movie, rom);
        engine.playToEnd();
        Console.WriteLine(engine.outcome().ToString());

        if (screen)
        {
            Console.Write(FramebufferSnapshot.capture(engine.machine).toAscii());
        }

        if (ppmPath is not null)
        {
            PpmImage.writeFile(ppmPath, PpmImage.fromMachine(engine.machine, Palette.byName(paletteName), scale));
            Console.WriteLine($"wrote frame to {ppmPath}");
        }

        if (verify)
        {
            bool deterministic = ReplayEngine.isDeterministic(movie, rom);
            Console.WriteLine("deterministic: " + (deterministic ? "yes" : "no"));
            return deterministic ? 0 : 1;
        }

        return 0;
    }

    static int info(string[] args)
    {
        if (args.Length < 1)
        {
            throw new ArgumentException("usage: chip8 replay info <movie> [--frames]");
        }

        bool listFrames = args.Length > 1 && args[1] == "--frames";
        if (args.Length > 1 && !listFrames)
        {
            throw new ArgumentException("unknown replay info option " + args[1]);
        }

        InputMovie movie = readMovie(args[0]);
        Console.WriteLine($"rom:       {movie.romName} ({RomHash.format(movie.romHash)})");
        Console.WriteLine($"platform:  {movie.platform}");
        Console.WriteLine($"quirks:    {QuirksCodec.toTokens(movie.quirks)}");
        Console.WriteLine($"seed:      {(movie.randomSeed.HasValue ? movie.randomSeed.Value.ToString() : "none")}");
        Console.WriteLine($"ipf:       {movie.instructionsPerFrame}");
        Console.WriteLine($"frames:    {movie.length} ({movie.length / 60.0:0.00}s at 60 Hz)");
        Console.WriteLine($"key input: {movie.entryCount} frame(s)");

        if (listFrames)
        {
            foreach (MovieFrame frame in movie.entries())
            {
                Console.WriteLine("  " + frame);
            }
        }
        return 0;
    }

    public static bool prefersBinary(string path) =>
        Path.GetExtension(path).Equals(".c8b", StringComparison.OrdinalIgnoreCase);

    public static void writeMovie(InputMovie movie, string path, bool binary)
    {
        if (binary)
        {
            File.WriteAllBytes(path, movie.toBytes());
        }
        else
        {
            File.WriteAllText(path, movie.toText());
        }
    }

    static InputMovie readMovie(string path)
    {
        if (!File.Exists(path))
        {
            throw new ArgumentException("movie not found: " + path);
        }

        byte[] bytes = File.ReadAllBytes(path);
        try
        {
            bool binary = bytes.Length >= 4 && bytes[0] == 'C' && bytes[1] == '8' && bytes[2] == 'M' && bytes[3] == 'V';
            return binary ? InputMovie.parseBytes(bytes) : InputMovie.parseText(Encoding.ASCII.GetString(bytes));
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or EndOfStreamException)
        {
            throw new ArgumentException(path + ": " + error.Message);
        }
    }

    static void printHelp()
    {
        Console.WriteLine("usage: chip8 replay record <rom> <script> -o <movie> [options]");
        Console.WriteLine("       chip8 replay play <movie> <rom> [options]");
        Console.WriteLine("       chip8 replay info <movie> [--frames]");
        Console.WriteLine("       chip8 <rom> --record <movie>      record live keyboard input in the window");
        Console.WriteLine();
        Console.WriteLine("A movie stores the platform, quirks, RNG seed, instructions per frame, the ROM hash and");
        Console.WriteLine("the keys held on every frame, so playback is frame-exact. Movies are text files; a .c8b");
        Console.WriteLine("extension or --binary selects the compact binary format. Both formats are read back.");
        Console.WriteLine();
        Console.WriteLine("record: drive the ROM headlessly with an input script and save the resulting movie.");
        Console.WriteLine("  script lines: press K | release K | hold K for N | wait N | frame N: <command>");
        Console.WriteLine("                assert vX ==|!= V | assert i|pc ==|!= V | assert pixel X Y [on|off]");
        Console.WriteLine("                assert exited | assert running        (# or // start a comment)");
        Console.WriteLine("  -o, --out PATH    movie file to write (required)");
        Console.WriteLine("  --platform NAME   chip8 (default), superchip, or xochip");
        Console.WriteLine("  --quirks NAME     auto (default), modern, cosmac, superchip, xochip");
        Console.WriteLine("  --seed N          RNG seed stored in the movie (default 1)");
        Console.WriteLine("  --ipf N           instructions per frame (default 11)");
        Console.WriteLine("  --binary          write the binary format");
        Console.WriteLine();
        Console.WriteLine("play: replay a movie and print frames, framebuffer hash, register hash, lit pixels.");
        Console.WriteLine("  --verify          replay twice and fail unless both runs end in the same state");
        Console.WriteLine("  --screen          print the final framebuffer as ASCII");
        Console.WriteLine("  --ppm PATH        export the final frame as a PPM image");
        Console.WriteLine("  --palette NAME    PPM palette: octo (default), mono, grayscale, extended16");
        Console.WriteLine("  --scale N         PPM / window pixel scale (default 14)");
        Console.WriteLine("  --window          watch the playback in the Raylib window");
        Console.WriteLine("  --force           play even if the ROM hash does not match the movie");
    }
}
