using System;

namespace Chip8.Core;

public sealed partial class Machine
{
    public const int displayWidth = 64;
    public const int displayHeight = 32;
    public const int displayPixels = displayWidth * displayHeight;
    public const int memorySize = 4096;
    public const int xoMemorySize = 65536;
    public const ushort programStart = 0x200;

    const int registerCount = 16;
    const int stackDepth = 16;
    const int keyCount = 16;
    const ushort fontStart = 0x50;
    const ushort bigFontStart = 0xA0;
    const int rplFlagCount = 16;
    const int audioPatternBytes = 16;

    public byte[] memory { get; }
    public byte[] v { get; } = new byte[registerCount];
    public ushort[] stack { get; } = new ushort[stackDepth];
    public bool[] display { get; } = new bool[displayPixels];
    public bool[] keypad { get; } = new bool[keyCount];

    public Platform platform { get; }
    public Video video { get; }

    public byte[] rplFlags { get; } = new byte[rplFlagCount];
    public byte[] audioPattern { get; } = new byte[audioPatternBytes];

    public ushort i { get; internal set; }
    public ushort pc { get; internal set; }
    public byte sp { get; internal set; }
    public byte delayTimer { get; internal set; }
    public byte soundTimer { get; internal set; }
    public int planeMask { get; internal set; }
    public byte audioPitch { get; internal set; }
    public bool exited { get; internal set; }

    public bool soundActive => soundTimer > 0;
    public bool hires => video.hires;
    public double audioFrequency => 4000.0 * Math.Pow(2.0, (audioPitch - 64) / 48.0);

    public bool drawPending { get; internal set; }

    public bool waitingForKey { get; private set; }

    public Quirks quirks { get; }

    readonly bool[] keypadPrevious = new bool[keyCount];
    readonly Random rng;

    public Machine(Quirks? quirks = null, int? randomSeed = null, Platform platform = Platform.Chip8)
    {
        this.platform = platform;
        this.quirks = quirks ?? defaultQuirks(platform);
        memory = new byte[platform == Platform.XoChip ? xoMemorySize : memorySize];
        video = new Video();
        rng = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        reset();
    }

    static Quirks defaultQuirks(Platform platform) => platform switch
    {
        Platform.SuperChip => Quirks.superChip(),
        Platform.XoChip => Quirks.xoChip(),
        _ => Quirks.modern(),
    };

    public void reset()
    {
        Array.Clear(memory, 0, memory.Length);
        Array.Clear(v, 0, v.Length);
        Array.Clear(stack, 0, stack.Length);
        Array.Clear(display, 0, display.Length);
        Array.Clear(keypad, 0, keypad.Length);
        Array.Clear(keypadPrevious, 0, keypadPrevious.Length);
        Array.Clear(audioPattern, 0, audioPattern.Length);
        Array.Copy(FontSet.glyphs, 0, memory, fontStart, FontSet.glyphs.Length);
        Array.Copy(FontSet.bigGlyphs, 0, memory, bigFontStart, FontSet.bigGlyphs.Length);
        video.reset();
        i = 0;
        pc = programStart;
        sp = 0;
        delayTimer = 0;
        soundTimer = 0;
        planeMask = 1;
        audioPitch = 64;
        exited = false;
        drawPending = false;
        waitingForKey = false;
    }

    public void loadRom(byte[] rom)
    {
        int capacity = memory.Length - programStart;
        if (rom.Length > capacity)
        {
            throw new ArgumentException(
                $"ROM is {rom.Length} bytes, only {capacity} fit above 0x{programStart:X3}.",
                nameof(rom));
        }

        reset();
        Array.Copy(rom, 0, memory, programStart, rom.Length);
    }

    public void step()
    {
        ushort opcode = (ushort)((memory[pc] << 8) | memory[pc + 1]);
        pc += 2;
        execute(opcode);
        Array.Copy(keypad, keypadPrevious, keyCount);
    }

    public void tickTimers()
    {
        if (delayTimer > 0)
        {
            delayTimer--;
        }

        if (soundTimer > 0)
        {
            soundTimer--;
        }
    }

    public void runFrame(int instructionsPerFrame)
    {
        for (int n = 0; n < instructionsPerFrame; n++)
        {
            step();
        }

        tickTimers();
    }

    public void pressKey(int key)
    {
        keypad[key & 0xF] = true;
    }

    public void releaseKey(int key)
    {
        keypad[key & 0xF] = false;
    }
}
