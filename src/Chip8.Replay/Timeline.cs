using System;
using Chip8.Core;

namespace Chip8.Replay;

public sealed class Timeline
{
    public const int defaultKeyframeInterval = 8;
    public const int defaultCapacity = 256;

    public Machine machine { get; }
    public InputMovie movie { get; }
    public RewindBuffer rewind { get; }
    public int keyframeInterval { get; }
    public int currentFrame { get; private set; }

    int nextKeyframeFrame;

    Timeline(Machine machine, InputMovie movie, int keyframeInterval, int capacity)
    {
        this.machine = machine;
        this.movie = movie;
        this.keyframeInterval = keyframeInterval;
        rewind = new RewindBuffer(capacity);
        currentFrame = 0;
        nextKeyframeFrame = 0;
    }

    public static Timeline forMovie(InputMovie movie, byte[] rom,
        int keyframeInterval = defaultKeyframeInterval, int capacity = defaultCapacity)
    {
        if (keyframeInterval < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keyframeInterval));
        }
        var machine = MachineBuilder.fromMovie(movie, rom);
        return new Timeline(machine, movie, keyframeInterval, capacity);
    }

    public static Timeline forMachine(Machine machine, InputMovie movie,
        int keyframeInterval = defaultKeyframeInterval, int capacity = defaultCapacity)
    {
        if (keyframeInterval < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keyframeInterval));
        }
        return new Timeline(machine, movie, keyframeInterval, capacity);
    }

    public int instructionsPerFrame => movie.instructionsPerFrame;

    public void advanceFrame()
    {
        maybeRecordKeyframe();
        stepForwardOnce();
    }

    public void advanceFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            advanceFrame();
        }
    }

    public void advanceTo(int frame)
    {
        if (frame < currentFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "advanceTo cannot move backward; use rewindTo");
        }
        while (currentFrame < frame)
        {
            advanceFrame();
        }
    }

    public void rewindTo(int frame)
    {
        if (frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
        if (frame > currentFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "rewindTo cannot move forward; use advanceTo");
        }
        if (frame == currentFrame)
        {
            return;
        }

        TimelineSnapshot? keyframe = rewind.nearestAtOrBefore(frame);
        if (keyframe is null)
        {
            throw new InvalidOperationException(
                "frame " + frame + " is older than the retained rewind window (oldest keyframe "
                + rewind.oldestFrame + ")");
        }

        machine.loadState(keyframe.state);
        currentFrame = keyframe.frameIndex;

        while (currentFrame < frame)
        {
            stepForwardOnce();
        }
    }

    public bool stepBack()
    {
        if (currentFrame == 0)
        {
            return false;
        }
        rewindTo(currentFrame - 1);
        return true;
    }

    public byte[] currentState() => machine.saveState();

    public ReplayOutcome outcome() => ReplayOutcome.capture(machine, currentFrame);

    void stepForwardOnce()
    {
        KeyMask injected = movie.keysAt(currentFrame);
        injected.applyTo(machine.keypad);
        machine.runFrame(instructionsPerFrame);
        currentFrame++;
    }

    void maybeRecordKeyframe()
    {
        if (currentFrame >= nextKeyframeFrame)
        {
            rewind.store(currentFrame, machine.saveState());
            nextKeyframeFrame = currentFrame + keyframeInterval;
        }
    }
}
