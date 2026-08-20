using Chip8.Core;

namespace Chip8.Replay;

public sealed class ScriptRunner
{
    public Machine machine { get; }
    public int instructionsPerFrame { get; }
    public int currentFrame { get; private set; }
    public KeyMask heldKeys { get; private set; }
    public int assertionsPassed { get; private set; }

    readonly InputMovie? record;
    int activeLine;

    public ScriptRunner(Machine machine, int instructionsPerFrame, InputMovie? record = null)
    {
        this.machine = machine;
        this.instructionsPerFrame = instructionsPerFrame;
        this.record = record;
        heldKeys = KeyMask.empty;
    }

    public static ScriptRunner forRom(byte[] rom, Platform platform, Quirks? quirks, int instructionsPerFrame, int? randomSeed = 1)
    {
        var machine = new Machine(quirks, randomSeed, platform);
        machine.loadRom(rom);
        return new ScriptRunner(machine, instructionsPerFrame);
    }

    public void run(InputScript script)
    {
        foreach (ScriptCommand command in script.commands)
        {
            activeLine = command.sourceLine;
            command.execute(this);
        }
    }

    public void press(int key)
    {
        heldKeys = heldKeys.withPressed(key);
    }

    public void release(int key)
    {
        heldKeys = heldKeys.withReleased(key);
    }

    public void advanceFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            heldKeys.applyTo(machine.keypad);
            record?.recordFrame(currentFrame, heldKeys);
            machine.runFrame(instructionsPerFrame);
            currentFrame++;
        }

        record?.extendLength(currentFrame);
    }

    public void advanceTo(int frame)
    {
        if (frame < currentFrame)
        {
            throw new ScriptException(activeLine,
                "cannot schedule frame " + frame + ", timeline is already at frame " + currentFrame);
        }
        advanceFrames(frame - currentFrame);
    }

    public void checkCompare(ScriptCommand command, string name, int actual, CompareOp op, int expected)
    {
        if (CompareOps.evaluate(op, actual, expected))
        {
            assertionsPassed++;
            return;
        }

        throw new ScriptAssertionException(command.sourceLine, currentFrame,
            "expected " + name + " " + CompareOps.symbol(op) + " " + expected + " but was " + actual);
    }

    public void checkPixel(AssertPixelCommand command)
    {
        FramebufferSnapshot snapshot = FramebufferSnapshot.capture(machine);
        if (command.x < 0 || command.y < 0 || command.x >= snapshot.width || command.y >= snapshot.height)
        {
            throw new ScriptAssertionException(command.sourceLine, currentFrame,
                "pixel (" + command.x + "," + command.y + ") is outside the "
                + snapshot.width + "x" + snapshot.height + " framebuffer");
        }

        bool lit = snapshot.isLit(command.x, command.y);
        if (lit == command.expectedLit)
        {
            assertionsPassed++;
            return;
        }

        throw new ScriptAssertionException(command.sourceLine, currentFrame,
            "expected pixel (" + command.x + "," + command.y + ") to be "
            + (command.expectedLit ? "on" : "off") + " but it was " + (lit ? "on" : "off"));
    }

    public void checkExited(AssertExitedCommand command)
    {
        if (machine.exited == command.expectedExited)
        {
            assertionsPassed++;
            return;
        }

        throw new ScriptAssertionException(command.sourceLine, currentFrame,
            "expected machine to be " + (command.expectedExited ? "exited" : "running")
            + " but exited=" + machine.exited);
    }
}
