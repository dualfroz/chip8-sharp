using Chip8.Core;

namespace Chip8.Replay;

public sealed class RomExpectation
{
    public string romName { get; }
    public Platform platform { get; }
    public Quirks quirks { get; }
    public int cycles { get; }
    public int instructionsPerFrame { get; }
    public int randomSeed { get; }
    public int minLitPixels { get; }
    public int maxLitPixels { get; }
    public ulong? framebufferHash { get; }
    public string description { get; }

    public RomExpectation(
        string romName,
        Platform platform,
        Quirks quirks,
        int cycles,
        int minLitPixels,
        int maxLitPixels,
        int instructionsPerFrame = 11,
        int randomSeed = 1,
        ulong? framebufferHash = null,
        string description = "")
    {
        this.romName = romName;
        this.platform = platform;
        this.quirks = quirks;
        this.cycles = cycles;
        this.minLitPixels = minLitPixels;
        this.maxLitPixels = maxLitPixels;
        this.instructionsPerFrame = instructionsPerFrame;
        this.randomSeed = randomSeed;
        this.framebufferHash = framebufferHash;
        this.description = description;
    }

    public bool litPixelsInRange(int litPixels) => litPixels >= minLitPixels && litPixels <= maxLitPixels;

    public RomExpectation withHash(ulong hash) => new RomExpectation(
        romName, platform, quirks, cycles, minLitPixels, maxLitPixels,
        instructionsPerFrame, randomSeed, hash, description);
}
