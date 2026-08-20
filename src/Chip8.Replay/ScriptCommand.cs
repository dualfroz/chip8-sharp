using Chip8.Core;

namespace Chip8.Replay;

public enum CompareOp
{
    Equal,
    NotEqual,
}

public abstract class ScriptCommand
{
    public int sourceLine { get; internal set; }

    public abstract void execute(ScriptRunner runner);

    public abstract string describe();

    public override string ToString() => describe();
}

public sealed class PressCommand : ScriptCommand
{
    public int key { get; }

    public PressCommand(int key)
    {
        this.key = key;
    }

    public override void execute(ScriptRunner runner) => runner.press(key);

    public override string describe() => "press " + key.ToString("X1");
}

public sealed class ReleaseCommand : ScriptCommand
{
    public int key { get; }

    public ReleaseCommand(int key)
    {
        this.key = key;
    }

    public override void execute(ScriptRunner runner) => runner.release(key);

    public override string describe() => "release " + key.ToString("X1");
}

public sealed class HoldCommand : ScriptCommand
{
    public int key { get; }
    public int frames { get; }

    public HoldCommand(int key, int frames)
    {
        this.key = key;
        this.frames = frames;
    }

    public override void execute(ScriptRunner runner)
    {
        runner.press(key);
        runner.advanceFrames(frames);
        runner.release(key);
    }

    public override string describe() => "hold " + key.ToString("X1") + " for " + frames;
}

public sealed class WaitCommand : ScriptCommand
{
    public int frames { get; }

    public WaitCommand(int frames)
    {
        this.frames = frames;
    }

    public override void execute(ScriptRunner runner) => runner.advanceFrames(frames);

    public override string describe() => "wait " + frames;
}

public sealed class FrameCommand : ScriptCommand
{
    public int targetFrame { get; }
    public ScriptCommand inner { get; }

    public FrameCommand(int targetFrame, ScriptCommand inner)
    {
        this.targetFrame = targetFrame;
        this.inner = inner;
    }

    public override void execute(ScriptRunner runner)
    {
        runner.advanceTo(targetFrame);
        inner.execute(runner);
    }

    public override string describe() => "frame " + targetFrame + ": " + inner.describe();
}

public sealed class AssertRegisterCommand : ScriptCommand
{
    public int register { get; }
    public CompareOp op { get; }
    public int expected { get; }

    public AssertRegisterCommand(int register, CompareOp op, int expected)
    {
        this.register = register;
        this.op = op;
        this.expected = expected;
    }

    public override void execute(ScriptRunner runner)
    {
        int actual = runner.machine.v[register];
        runner.checkCompare(this, "V" + register.ToString("X1"), actual, op, expected);
    }

    public override string describe() =>
        "assert v" + register.ToString("X1") + " " + CompareOps.symbol(op) + " " + expected;
}

public sealed class AssertIndexCommand : ScriptCommand
{
    public CompareOp op { get; }
    public int expected { get; }

    public AssertIndexCommand(CompareOp op, int expected)
    {
        this.op = op;
        this.expected = expected;
    }

    public override void execute(ScriptRunner runner) =>
        runner.checkCompare(this, "I", runner.machine.i, op, expected);

    public override string describe() => "assert i " + CompareOps.symbol(op) + " " + expected;
}

public sealed class AssertPcCommand : ScriptCommand
{
    public CompareOp op { get; }
    public int expected { get; }

    public AssertPcCommand(CompareOp op, int expected)
    {
        this.op = op;
        this.expected = expected;
    }

    public override void execute(ScriptRunner runner) =>
        runner.checkCompare(this, "PC", runner.machine.pc, op, expected);

    public override string describe() => "assert pc " + CompareOps.symbol(op) + " " + expected;
}

public sealed class AssertPixelCommand : ScriptCommand
{
    public int x { get; }
    public int y { get; }
    public bool expectedLit { get; }

    public AssertPixelCommand(int x, int y, bool expectedLit)
    {
        this.x = x;
        this.y = y;
        this.expectedLit = expectedLit;
    }

    public override void execute(ScriptRunner runner) => runner.checkPixel(this);

    public override string describe() =>
        "assert pixel " + x + " " + y + " " + (expectedLit ? "on" : "off");
}

public sealed class AssertExitedCommand : ScriptCommand
{
    public bool expectedExited { get; }

    public AssertExitedCommand(bool expectedExited)
    {
        this.expectedExited = expectedExited;
    }

    public override void execute(ScriptRunner runner) => runner.checkExited(this);

    public override string describe() => expectedExited ? "assert exited" : "assert running";
}

public static class CompareOps
{
    public static string symbol(CompareOp op) => op == CompareOp.Equal ? "==" : "!=";

    public static bool evaluate(CompareOp op, int left, int right) =>
        op == CompareOp.Equal ? left == right : left != right;
}
