using System;
using System.Globalization;
using System.IO;

namespace Chip8.Cli;

static class CliArgs
{
    public static bool isHelp(string[] args) =>
        args.Length > 0 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help");

    public static string value(string[] args, ref int index)
    {
        string option = args[index];
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("missing value for " + option);
        }
        return args[++index];
    }

    public static int integer(string[] args, ref int index)
    {
        string option = args[index];
        string text = value(args, ref index);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new ArgumentException("expected a number for " + option + " but got '" + text + "'");
        }
        return parsed;
    }

    public static int positive(string[] args, ref int index)
    {
        string option = args[index];
        int parsed = integer(args, ref index);
        if (parsed < 1)
        {
            throw new ArgumentException(option + " must be at least 1");
        }
        return parsed;
    }

    public static byte[] readRom(string path)
    {
        if (!File.Exists(path))
        {
            throw new ArgumentException("ROM not found: " + path);
        }
        return File.ReadAllBytes(path);
    }
}
