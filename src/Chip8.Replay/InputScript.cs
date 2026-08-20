using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chip8.Replay;

public sealed class InputScript
{
    readonly List<ScriptCommand> commandList;

    InputScript(List<ScriptCommand> commands)
    {
        commandList = commands;
    }

    public IReadOnlyList<ScriptCommand> commands => commandList;

    public int count => commandList.Count;

    public static InputScript parse(string text)
    {
        var commands = new List<ScriptCommand>();
        string[] lines = text.Split('\n');

        for (int number = 1; number <= lines.Length; number++)
        {
            string stripped = stripComment(lines[number - 1]).Trim();
            if (stripped.Length == 0)
            {
                continue;
            }

            ScriptCommand command = parseLine(stripped, number);
            command.sourceLine = number;
            commands.Add(command);
        }

        return new InputScript(commands);
    }

    static string stripComment(string line)
    {
        int hash = line.IndexOf('#');
        int slashes = line.IndexOf("//", StringComparison.Ordinal);
        int cut = -1;
        if (hash >= 0)
        {
            cut = hash;
        }
        if (slashes >= 0 && (cut < 0 || slashes < cut))
        {
            cut = slashes;
        }
        return cut < 0 ? line : line.Substring(0, cut);
    }

    static ScriptCommand parseLine(string line, int number)
    {
        string[] head = splitWords(line);
        if (head.Length > 0 && head[0].Equals("frame", StringComparison.OrdinalIgnoreCase))
        {
            return parseFrame(line, number);
        }
        return parseSimple(head, number);
    }

    static ScriptCommand parseFrame(string line, int number)
    {
        int colon = line.IndexOf(':');
        if (colon < 0)
        {
            throw new ScriptParseException(number, "'frame' needs a ':' before the command, e.g. 'frame 10: press 5'");
        }

        string[] prefix = splitWords(line.Substring(0, colon));
        if (prefix.Length != 2)
        {
            throw new ScriptParseException(number, "expected 'frame <n>:'");
        }

        int target = parseNumber(prefix[1], number);
        if (target < 0)
        {
            throw new ScriptParseException(number, "frame index cannot be negative");
        }

        string innerText = line.Substring(colon + 1).Trim();
        if (innerText.Length == 0)
        {
            throw new ScriptParseException(number, "'frame <n>:' must be followed by a command");
        }

        string[] innerWords = splitWords(innerText);
        if (innerWords.Length > 0 && innerWords[0].Equals("frame", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptParseException(number, "'frame' commands cannot be nested");
        }

        ScriptCommand inner = parseSimple(innerWords, number);
        inner.sourceLine = number;
        return new FrameCommand(target, inner);
    }

    static ScriptCommand parseSimple(string[] words, int number)
    {
        if (words.Length == 0)
        {
            throw new ScriptParseException(number, "empty command");
        }

        string keyword = words[0].ToLowerInvariant();
        switch (keyword)
        {
            case "press":
                requireArity(words, 2, number, "press <key>");
                return new PressCommand(parseKey(words[1], number));

            case "release":
                requireArity(words, 2, number, "release <key>");
                return new ReleaseCommand(parseKey(words[1], number));

            case "hold":
                if (words.Length != 4 || !words[2].Equals("for", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ScriptParseException(number, "expected 'hold <key> for <frames>'");
                }
                return new HoldCommand(parseKey(words[1], number), parsePositive(words[3], number));

            case "wait":
            case "step":
                requireArity(words, 2, number, keyword + " <frames>");
                return new WaitCommand(parsePositive(words[1], number));

            case "assert":
                return parseAssert(words, number);

            default:
                throw new ScriptParseException(number, "unknown command '" + words[0] + "'");
        }
    }

    static ScriptCommand parseAssert(string[] words, int number)
    {
        if (words.Length < 2)
        {
            throw new ScriptParseException(number, "'assert' needs a target");
        }

        string target = words[1].ToLowerInvariant();

        if (target == "exited")
        {
            return new AssertExitedCommand(true);
        }
        if (target == "running")
        {
            return new AssertExitedCommand(false);
        }
        if (target == "pixel")
        {
            return parseAssertPixel(words, number);
        }
        if (target == "i")
        {
            var (op, value) = parseComparison(words, 2, number, 0xFFFF);
            return new AssertIndexCommand(op, value);
        }
        if (target == "pc")
        {
            var (op, value) = parseComparison(words, 2, number, 0xFFFF);
            return new AssertPcCommand(op, value);
        }
        if (target.Length == 2 && (target[0] == 'v'))
        {
            int register = parseNibble(target[1], number);
            var (op, value) = parseComparison(words, 2, number, 0xFF);
            return new AssertRegisterCommand(register, op, value);
        }

        throw new ScriptParseException(number, "unknown assert target '" + words[1] + "'");
    }

    static ScriptCommand parseAssertPixel(string[] words, int number)
    {
        if (words.Length < 4 || words.Length > 5)
        {
            throw new ScriptParseException(number, "expected 'assert pixel <x> <y> [on|off]'");
        }

        int x = parsePositive(words[2], number);
        int y = parsePositive(words[3], number);
        bool expectedLit = true;
        if (words.Length == 5)
        {
            string state = words[4].ToLowerInvariant();
            if (state == "off")
            {
                expectedLit = false;
            }
            else if (state != "on")
            {
                throw new ScriptParseException(number, "pixel state must be 'on' or 'off'");
            }
        }

        return new AssertPixelCommand(x, y, expectedLit);
    }

    static (CompareOp op, int value) parseComparison(string[] words, int start, int number, int max)
    {
        if (words.Length != start + 2)
        {
            throw new ScriptParseException(number, "expected '<target> <op> <value>' with op == or !=");
        }

        CompareOp op = parseOp(words[start], number);
        int value = parseNumber(words[start + 1], number);
        if (value < 0 || value > max)
        {
            throw new ScriptParseException(number, "value " + value + " out of range 0.." + max);
        }
        return (op, value);
    }

    static void requireArity(string[] words, int expected, int number, string usage)
    {
        if (words.Length != expected)
        {
            throw new ScriptParseException(number, "expected '" + usage + "'");
        }
    }

    static CompareOp parseOp(string token, int number) => token switch
    {
        "==" => CompareOp.Equal,
        "!=" => CompareOp.NotEqual,
        _ => throw new ScriptParseException(number, "unknown comparison '" + token + "', use == or !="),
    };

    static int parseKey(string token, int number)
    {
        int key = token.Length == 1 && isHexDigit(token[0])
            ? parseNibble(token[0], number)
            : parseNumber(token, number);
        if (key < 0 || key > 15)
        {
            throw new ScriptParseException(number, "key " + key + " out of range 0..15");
        }
        return key;
    }

    static int parsePositive(string token, int number)
    {
        int value = parseNumber(token, number);
        if (value < 0)
        {
            throw new ScriptParseException(number, "value cannot be negative");
        }
        return value;
    }

    static int parseNumber(string token, int number)
    {
        bool negative = token.StartsWith("-", StringComparison.Ordinal);
        string body = negative ? token.Substring(1) : token;
        int magnitude;

        if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(body.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out magnitude))
            {
                throw new ScriptParseException(number, "invalid hex number '" + token + "'");
            }
        }
        else if (!int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out magnitude))
        {
            throw new ScriptParseException(number, "invalid number '" + token + "'");
        }

        return negative ? -magnitude : magnitude;
    }

    static int parseNibble(char c, int number)
    {
        if (!isHexDigit(c))
        {
            throw new ScriptParseException(number, "'" + c + "' is not a hex digit");
        }
        return Convert.ToInt32(c.ToString(), 16);
    }

    static bool isHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    static string[] splitWords(string line) =>
        line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
}
