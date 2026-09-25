using System;
using Chip8.Core;

namespace Chip8.Cli;

sealed class Options
{
    public string romPath = "";
    public bool headless;
    public int cycles = 2000;
    public int scale = 14;
    public int instructionsPerFrame = 11;
    public int? seed;
    public string quirksProfile = "auto";
    public string platformName = "chip8";
    public string paletteName = "octo";
    public int maxFrames;
    public string? screenshotPath;
    public string? dumpPpmPath;
    public string? dumpWavPath;
    public string? recordPath;

    public Platform buildPlatform() => platformFrom(platformName);

    public Quirks? buildQuirks() => quirksFrom(quirksProfile);

    public static Platform platformFrom(string name) => name.ToLowerInvariant() switch
    {
        "superchip" or "schip" => Platform.SuperChip,
        "xochip" or "xo" => Platform.XoChip,
        _ => Platform.Chip8,
    };

    public static Quirks? quirksFrom(string name) => name.ToLowerInvariant() switch
    {
        "auto" => null,
        "cosmac" => Quirks.cosmacVip(),
        "superchip" => Quirks.superChip(),
        "xochip" => Quirks.xoChip(),
        _ => Quirks.modern(),
    };

    public Palette buildPalette() => Palette.byName(paletteName);

    public static Options parse(string[] args)
    {
        var options = new Options();
        int index = 0;

        while (index < args.Length)
        {
            string token = args[index];
            switch (token)
            {
                case "--headless":
                    options.headless = true;
                    index++;
                    break;
                case "--cycles":
                    options.cycles = int.Parse(args[++index]);
                    index++;
                    break;
                case "--scale":
                    options.scale = int.Parse(args[++index]);
                    index++;
                    break;
                case "--ipf":
                    options.instructionsPerFrame = int.Parse(args[++index]);
                    index++;
                    break;
                case "--seed":
                    options.seed = int.Parse(args[++index]);
                    index++;
                    break;
                case "--quirks":
                    options.quirksProfile = args[++index];
                    index++;
                    break;
                case "--platform":
                    options.platformName = args[++index];
                    index++;
                    break;
                case "--palette":
                    options.paletteName = args[++index];
                    index++;
                    break;
                case "--frames":
                    options.maxFrames = int.Parse(args[++index]);
                    index++;
                    break;
                case "--shot":
                    options.screenshotPath = args[++index];
                    index++;
                    break;
                case "--ppm":
                    options.dumpPpmPath = args[++index];
                    index++;
                    break;
                case "--wav":
                    options.dumpWavPath = args[++index];
                    index++;
                    break;
                case "--record":
                    options.recordPath = CliArgs.value(args, ref index);
                    index++;
                    break;
                default:
                    if (token.StartsWith("--"))
                    {
                        throw new ArgumentException($"Unknown option: {token}");
                    }
                    options.romPath = token;
                    index++;
                    break;
            }
        }

        if (options.romPath.Length == 0)
        {
            throw new ArgumentException("No ROM path given.");
        }

        return options;
    }
}
