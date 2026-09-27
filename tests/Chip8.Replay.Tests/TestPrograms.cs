using Chip8.Core;
using Chip8.Core.Assembly;

namespace Chip8.Replay.Tests;

static class TestPrograms
{
    public const int instructionsPerFrame = 11;

    public static readonly byte[] keyMirror = Assembler.assembleRom(@"
        LD V3, 0
        LD F, V3
        DRW V3, V3, 5
start:
        ADD V4, 1
        LD V0, 5
        SKP V0
        JP up
        LD V1, 1
        JP start
up:
        LD V1, 0
        JP start
");

    public static readonly byte[] randomWalker = Assembler.assembleRom(@"
        LD I, dot
loop:
        LD V0, 5
        SKNP V0
        RND V1, 0x3F
        RND V2, 0x1F
        DRW V1, V2, 1
        JP loop
dot:
        DB 0x80
");

    public static readonly byte[] exitsAfterDrawing = Assembler.assembleRom(@"
        HIGH
        LD V0, 100
        LD V1, 40
        LD I, block
        DRW V0, V1, 2
        EXIT
block:
        DB 0xFF, 0xFF
");

    public static Machine machineFor(byte[] rom, Platform platform = Platform.Chip8, int seed = 1)
    {
        var machine = new Machine(null, seed, platform);
        machine.loadRom(rom);
        return machine;
    }

    public static InputMovie movieFor(byte[] rom, Platform platform = Platform.Chip8, int seed = 1) =>
        MachineBuilder.headerFor(machineFor(rom, platform, seed), rom, "test.ch8", instructionsPerFrame, seed);
}
