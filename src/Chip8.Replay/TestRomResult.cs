using System.Text;

namespace Chip8.Replay;

public sealed class TestRomResult
{
    public string romName { get; }
    public bool passed { get; }
    public int litPixels { get; }
    public int minLitPixels { get; }
    public int maxLitPixels { get; }
    public ulong framebufferHash { get; }
    public ulong registerHash { get; }
    public int cyclesExecuted { get; }
    public bool exited { get; }
    public string detail { get; }

    public TestRomResult(
        string romName,
        bool passed,
        int litPixels,
        int minLitPixels,
        int maxLitPixels,
        ulong framebufferHash,
        ulong registerHash,
        int cyclesExecuted,
        bool exited,
        string detail)
    {
        this.romName = romName;
        this.passed = passed;
        this.litPixels = litPixels;
        this.minLitPixels = minLitPixels;
        this.maxLitPixels = maxLitPixels;
        this.framebufferHash = framebufferHash;
        this.registerHash = registerHash;
        this.cyclesExecuted = cyclesExecuted;
        this.exited = exited;
        this.detail = detail;
    }

    public string format()
    {
        var builder = new StringBuilder();
        builder.Append(passed ? "PASS " : "FAIL ");
        builder.Append(romName);
        builder.Append(" lit=").Append(litPixels);
        builder.Append(" range=[").Append(minLitPixels).Append("..").Append(maxLitPixels).Append(']');
        builder.Append(" fb=").Append(FramebufferHash.format(framebufferHash));
        builder.Append(" cycles=").Append(cyclesExecuted);
        if (detail.Length > 0)
        {
            builder.Append(" (").Append(detail).Append(')');
        }
        return builder.ToString();
    }

    public override string ToString() => format();
}
