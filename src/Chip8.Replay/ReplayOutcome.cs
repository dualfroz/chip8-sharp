using Chip8.Core;

namespace Chip8.Replay;

public readonly struct ReplayOutcome
{
    public int frames { get; }
    public ulong framebufferHash { get; }
    public ulong registerHash { get; }
    public int litPixels { get; }
    public bool exited { get; }

    public ReplayOutcome(int frames, ulong framebufferHash, ulong registerHash, int litPixels, bool exited)
    {
        this.frames = frames;
        this.framebufferHash = framebufferHash;
        this.registerHash = registerHash;
        this.litPixels = litPixels;
        this.exited = exited;
    }

    public static ReplayOutcome capture(Machine machine, int frames)
    {
        var snapshot = FramebufferSnapshot.capture(machine);
        return new ReplayOutcome(
            frames,
            FramebufferHash.compute(snapshot),
            FramebufferHash.computeWithRegisters(machine),
            snapshot.litCount,
            machine.exited);
    }

    public bool sameAs(ReplayOutcome other) =>
        frames == other.frames
        && framebufferHash == other.framebufferHash
        && registerHash == other.registerHash
        && litPixels == other.litPixels
        && exited == other.exited;

    public override string ToString() =>
        "frames=" + frames
        + " fb=" + FramebufferHash.format(framebufferHash)
        + " reg=" + FramebufferHash.format(registerHash)
        + " lit=" + litPixels
        + " exited=" + exited;
}
