using System;
using System.IO;
using Chip8.Core;
using Chip8.Core.Assembly;

namespace Chip8.Tests;

static class TestHarness
{
    static string romDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "roms")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("Could not locate the 'roms' test directory.");
        }

        return Path.Combine(dir.FullName, "roms");
    }

    public static byte[] romBytes(string name) => File.ReadAllBytes(Path.Combine(romDirectory(), name));

    public static byte[] assembledRom(string sourceName) =>
        Assembler.assembleRom(File.ReadAllText(Path.Combine(romDirectory(), sourceName)));

    public static string goldenText(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "goldens", name));

    public static Machine withOpcode(ushort opcode, Quirks? quirks = null)
    {
        var machine = new Machine(quirks ?? Quirks.modern(), randomSeed: 1);
        machine.memory[Machine.programStart] = (byte)(opcode >> 8);
        machine.memory[Machine.programStart + 1] = (byte)(opcode & 0xFF);
        return machine;
    }

    public static Machine withOpcode(ushort opcode, Platform platform, Quirks? quirks = null)
    {
        var machine = new Machine(quirks, randomSeed: 1, platform);
        machine.memory[Machine.programStart] = (byte)(opcode >> 8);
        machine.memory[Machine.programStart + 1] = (byte)(opcode & 0xFF);
        return machine;
    }

    public static Machine withProgram(Platform platform, params ushort[] words)
    {
        var machine = new Machine(null, randomSeed: 1, platform);
        for (int index = 0; index < words.Length; index++)
        {
            machine.memory[Machine.programStart + index * 2] = (byte)(words[index] >> 8);
            machine.memory[Machine.programStart + index * 2 + 1] = (byte)(words[index] & 0xFF);
        }
        return machine;
    }

    public const int instructionsPerFrame = 11;

    public static Machine runRom(string name, int cycles, Quirks? quirks = null) =>
        runProgram(romBytes(name), cycles, quirks);

    public static Machine runProgram(byte[] rom, int cycles, Quirks? quirks = null)
    {
        var machine = new Machine(quirks ?? Quirks.modern(), randomSeed: 1);
        machine.loadRom(rom);
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            machine.step();
            if (cycle % instructionsPerFrame == instructionsPerFrame - 1)
            {
                machine.tickTimers();
            }
        }
        return machine;
    }

    public static Machine runRomExtended(string name, int cycles, Platform platform, Quirks? quirks = null)
    {
        var machine = new Machine(quirks, randomSeed: 1, platform);
        machine.loadRom(romBytes(name));
        for (int cycle = 0; cycle < cycles && !machine.exited; cycle++)
        {
            machine.step();
            if (cycle % instructionsPerFrame == instructionsPerFrame - 1)
            {
                machine.tickTimers();
            }
        }
        return machine;
    }
}
