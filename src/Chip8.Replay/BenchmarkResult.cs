using System.Globalization;
using System.Text;

namespace Chip8.Replay;

public sealed class BenchmarkResult
{
    public string romName { get; }
    public int stepsExecuted { get; }
    public int framesExecuted { get; }
    public double elapsedMilliseconds { get; }
    public ulong framebufferHash { get; }
    public bool exited { get; }

    public BenchmarkResult(
        string romName,
        int stepsExecuted,
        int framesExecuted,
        double elapsedMilliseconds,
        ulong framebufferHash,
        bool exited)
    {
        this.romName = romName;
        this.stepsExecuted = stepsExecuted;
        this.framesExecuted = framesExecuted;
        this.elapsedMilliseconds = elapsedMilliseconds;
        this.framebufferHash = framebufferHash;
        this.exited = exited;
    }

    public double elapsedSeconds => elapsedMilliseconds / 1000.0;

    public double instructionsPerSecond =>
        elapsedMilliseconds > 0 ? stepsExecuted / elapsedSeconds : double.NaN;

    public double framesPerSecond =>
        elapsedMilliseconds > 0 ? framesExecuted / elapsedSeconds : double.NaN;

    public string format()
    {
        var builder = new StringBuilder();
        builder.Append(romName).Append(": ");
        builder.Append(stepsExecuted).Append(" steps / ");
        builder.Append(framesExecuted).Append(" frames in ");
        builder.Append(elapsedMilliseconds.ToString("F2", CultureInfo.InvariantCulture)).Append(" ms");
        if (elapsedMilliseconds > 0)
        {
            builder.Append(" (");
            builder.Append((instructionsPerSecond / 1_000_000.0).ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" Mips, ");
            builder.Append(framesPerSecond.ToString("F0", CultureInfo.InvariantCulture));
            builder.Append(" fps)");
        }
        return builder.ToString();
    }

    public override string ToString() => format();
}
