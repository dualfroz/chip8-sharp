using System;
using Chip8.Core;

namespace Chip8.Replay;

public enum ReplayMode
{
    Record,
    Playback,
}

public sealed class ReplayEngine
{
    public Machine machine { get; }
    public InputMovie movie { get; }
    public ReplayMode mode { get; }
    public int instructionsPerFrame { get; }
    public int frameIndex { get; private set; }

    ReplayEngine(Machine machine, InputMovie movie, ReplayMode mode)
    {
        this.machine = machine;
        this.movie = movie;
        this.mode = mode;
        instructionsPerFrame = movie.instructionsPerFrame;
    }

    public static ReplayEngine record(Machine machine, InputMovie movie) =>
        new ReplayEngine(machine, movie, ReplayMode.Record);

    public static ReplayEngine playback(InputMovie movie, byte[] rom) =>
        new ReplayEngine(MachineBuilder.fromMovie(movie, rom), movie, ReplayMode.Playback);

    public static ReplayEngine playback(InputMovie movie, Machine machine) =>
        new ReplayEngine(machine, movie, ReplayMode.Playback);

    public bool finished => frameIndex >= movie.length || machine.exited;

    public void pressKey(int key)
    {
        requireRecord();
        machine.pressKey(key);
    }

    public void releaseKey(int key)
    {
        requireRecord();
        machine.releaseKey(key);
    }

    public void setKeys(KeyMask keys)
    {
        requireRecord();
        keys.applyTo(machine.keypad);
    }

    public void advanceFrame()
    {
        if (mode == ReplayMode.Record)
        {
            KeyMask observed = KeyMask.fromKeypad(machine.keypad);
            movie.recordFrame(frameIndex, observed);
            machine.runFrame(instructionsPerFrame);
        }
        else
        {
            KeyMask injected = movie.keysAt(frameIndex);
            injected.applyTo(machine.keypad);
            machine.runFrame(instructionsPerFrame);
        }

        frameIndex++;

        if (mode == ReplayMode.Record)
        {
            movie.extendLength(frameIndex);
        }
    }

    public void advanceFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            advanceFrame();
        }
    }

    public void playToEnd()
    {
        while (!finished)
        {
            advanceFrame();
        }
    }

    public ReplayOutcome outcome() => ReplayOutcome.capture(machine, frameIndex);

    public static ReplayOutcome replayToEnd(InputMovie movie, byte[] rom)
    {
        var engine = playback(movie, rom);
        engine.playToEnd();
        return engine.outcome();
    }

    public static bool isDeterministic(InputMovie movie, byte[] rom)
    {
        ReplayOutcome first = replayToEnd(movie, rom);
        ReplayOutcome second = replayToEnd(movie, rom);
        return first.sameAs(second);
    }

    void requireRecord()
    {
        if (mode != ReplayMode.Record)
        {
            throw new InvalidOperationException("input can only be supplied while recording");
        }
    }
}
