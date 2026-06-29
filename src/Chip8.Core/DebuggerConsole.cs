using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chip8.Core;

public sealed class DebuggerConsole
{
    const int defaultRunSteps = 1_000_000;

    public DebuggerConsole(Debugger debugger)
    {
        this.debugger = debugger;
    }

    public Debugger debugger { get; }

    public bool quitRequested { get; private set; }

    public string execute(string line)
    {
        string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        string command = parts[0].ToLowerInvariant();
        return command switch
        {
            "help" or "h" or "?" => help(),
            "s" or "step" => step(parts),
            "so" => stepOver(),
            "c" or "cont" or "continue" => run(parts),
            "u" or "until" => runUntil(parts),
            "b" or "break" => addBreakpoint(parts),
            "rb" or "delete" => removeBreakpoint(parts),
            "bl" => listBreakpoints(),
            "wm" => watchMemory(parts),
            "wr" => watchRegister(parts),
            "wl" => listWatches(),
            "wc" => clearWatches(),
            "r" or "regs" => registers(),
            "mem" or "x" => memory(parts),
            "dis" or "d" => disassemble(parts),
            "reset" => reset(),
            "q" or "quit" or "exit" => quit(),
            _ => "unknown command '" + command + "' (try 'help')",
        };
    }

    static string help() => string.Join('\n', new[]
    {
        "commands:",
        "  s [n]        step n instructions (default 1)",
        "  so           step over a CALL",
        "  c [n]        continue up to n steps (default until stop)",
        "  u <addr>     run until pc reaches addr",
        "  b <addr>     add breakpoint",
        "  rb <addr>    remove breakpoint",
        "  bl           list breakpoints",
        "  wm <addr>    watch a memory byte",
        "  wr <Vx>      watch a register",
        "  wl           list watchpoints",
        "  wc           clear watchpoints",
        "  r            show registers",
        "  mem <a> [n]  hex dump n bytes from a (default 16)",
        "  dis [n]      disassemble n instructions from pc (default 8)",
        "  reset        reset the machine",
        "  q            quit",
    });

    string step(string[] parts)
    {
        int count = optionalInt(parts, 1, 1);
        for (int taken = 0; taken < count && !debugger.machine.exited; taken++)
        {
            debugger.step();
        }
        return registers();
    }

    string stepOver()
    {
        RunResult outcome = debugger.stepOver(defaultRunSteps);
        return describe(outcome) + "\n" + registers();
    }

    string run(string[] parts)
    {
        int limit = optionalInt(parts, 1, defaultRunSteps);
        RunResult outcome = debugger.run(limit);
        return describe(outcome) + "\n" + registers();
    }

    string runUntil(string[] parts)
    {
        if (!tryAddress(parts, out ushort address))
        {
            return "usage: u <addr>";
        }
        RunResult outcome = debugger.runUntil(address, defaultRunSteps);
        return describe(outcome) + "\n" + registers();
    }

    string addBreakpoint(string[] parts)
    {
        if (!tryAddress(parts, out ushort address))
        {
            return "usage: b <addr>";
        }
        debugger.addBreakpoint(address);
        return "breakpoint set at 0x" + address.ToString("X4", CultureInfo.InvariantCulture);
    }

    string removeBreakpoint(string[] parts)
    {
        if (!tryAddress(parts, out ushort address))
        {
            return "usage: rb <addr>";
        }
        debugger.removeBreakpoint(address);
        return "breakpoint cleared at 0x" + address.ToString("X4", CultureInfo.InvariantCulture);
    }

    string listBreakpoints()
    {
        var addresses = new List<ushort>(debugger.breakpointAddresses);
        if (addresses.Count == 0)
        {
            return "no breakpoints";
        }
        addresses.Sort();
        var builder = new StringBuilder("breakpoints:");
        foreach (ushort address in addresses)
        {
            builder.Append(" 0x").Append(address.ToString("X4", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    string watchMemory(string[] parts)
    {
        if (!tryAddress(parts, out ushort address))
        {
            return "usage: wm <addr>";
        }
        debugger.watchMemory(address);
        return "watching memory 0x" + address.ToString("X4", CultureInfo.InvariantCulture);
    }

    string watchRegister(string[] parts)
    {
        if (parts.Length < 2 || !tryRegister(parts[1], out int register))
        {
            return "usage: wr <Vx>";
        }
        debugger.watchRegister(register);
        return "watching register V" + register.ToString("X1", CultureInfo.InvariantCulture);
    }

    string listWatches()
    {
        var builder = new StringBuilder();
        foreach (ushort address in debugger.watchedMemory)
        {
            builder.Append("mem 0x").Append(address.ToString("X4", CultureInfo.InvariantCulture)).Append('\n');
        }
        foreach (int register in debugger.watchedRegisters)
        {
            builder.Append("reg V").Append(register.ToString("X1", CultureInfo.InvariantCulture)).Append('\n');
        }
        return builder.Length == 0 ? "no watchpoints" : builder.ToString().TrimEnd('\n');
    }

    string clearWatches()
    {
        debugger.clearWatches();
        return "watchpoints cleared";
    }

    string registers() => debugger.snapshot().format().TrimEnd('\n');

    string memory(string[] parts)
    {
        if (!tryAddress(parts, out ushort address))
        {
            return "usage: mem <addr> [len]";
        }
        int length = optionalInt(parts, 2, 16);
        byte[] window = debugger.readMemory(address, length);

        var builder = new StringBuilder();
        for (int offset = 0; offset < length; offset += 16)
        {
            builder.Append("0x").Append(((address + offset) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture)).Append(':');
            for (int column = 0; column < 16 && offset + column < length; column++)
            {
                builder.Append(' ').Append(window[offset + column].ToString("X2", CultureInfo.InvariantCulture));
            }
            builder.Append('\n');
        }
        return builder.ToString().TrimEnd('\n');
    }

    string disassemble(string[] parts)
    {
        int count = optionalInt(parts, 1, 8);
        var builder = new StringBuilder();
        foreach (DecodedInstruction line in debugger.disassembleWindow(count))
        {
            builder.Append(line.address == debugger.machine.pc ? "-> " : "   ");
            builder.Append(line).Append('\n');
        }
        return builder.ToString().TrimEnd('\n');
    }

    string reset()
    {
        debugger.machine.reset();
        return "machine reset";
    }

    string quit()
    {
        quitRequested = true;
        return "bye";
    }

    static string describe(RunResult outcome) => outcome.reason switch
    {
        StopReason.Breakpoint => "stopped at breakpoint 0x" + outcome.triggeredAddress.ToString("X4", CultureInfo.InvariantCulture) + " after " + outcome.steps + " steps",
        StopReason.Watchpoint => "stopped on watchpoint after " + outcome.steps + " steps",
        StopReason.Exited => "interpreter exited after " + outcome.steps + " steps",
        _ => "ran " + outcome.steps + " steps",
    };

    static int optionalInt(string[] parts, int index, int fallback)
    {
        if (parts.Length <= index)
        {
            return fallback;
        }
        return int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }

    static bool tryAddress(string[] parts, out ushort address)
    {
        address = 0;
        if (parts.Length < 2)
        {
            return false;
        }
        if (!tryParseNumber(parts[1], out int value))
        {
            return false;
        }
        address = (ushort)(value & 0xFFFF);
        return true;
    }

    static bool tryRegister(string token, out int register)
    {
        register = 0;
        string trimmed = token.StartsWith("V", StringComparison.OrdinalIgnoreCase) ? token.Substring(1) : token;
        if (!int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value) || value is < 0 or > 0xF)
        {
            return false;
        }
        register = value;
        return true;
    }

    static bool tryParseNumber(string token, out int value)
    {
        string digits = token;
        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits.Substring(2);
        }
        else if (digits.StartsWith("$", StringComparison.Ordinal))
        {
            digits = digits.Substring(1);
        }
        return int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
