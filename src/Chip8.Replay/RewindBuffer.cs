using System;

namespace Chip8.Replay;

public sealed class RewindBuffer
{
    readonly TimelineSnapshot?[] ring;
    int head;
    int count;
    long memoryBytes;

    public RewindBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "rewind buffer needs at least one slot");
        }
        ring = new TimelineSnapshot?[capacity];
    }

    public int capacity => ring.Length;

    public int keyframeCount => count;

    public long memoryUsed => memoryBytes;

    public bool isEmpty => count == 0;

    public void store(int frameIndex, byte[] state)
    {
        var snapshot = new TimelineSnapshot(frameIndex, state);

        if (count == ring.Length)
        {
            TimelineSnapshot evicted = ring[head]!;
            memoryBytes -= evicted.stateSize;
            ring[head] = snapshot;
            head = (head + 1) % ring.Length;
        }
        else
        {
            int slot = (head + count) % ring.Length;
            ring[slot] = snapshot;
            count++;
        }

        memoryBytes += snapshot.stateSize;
    }

    public TimelineSnapshot? nearestAtOrBefore(int frameIndex)
    {
        TimelineSnapshot? best = null;
        for (int offset = 0; offset < count; offset++)
        {
            TimelineSnapshot candidate = ring[(head + offset) % ring.Length]!;
            if (candidate.frameIndex <= frameIndex && (best is null || candidate.frameIndex > best.frameIndex))
            {
                best = candidate;
            }
        }
        return best;
    }

    public int oldestFrame
    {
        get
        {
            if (count == 0)
            {
                return -1;
            }
            int oldest = int.MaxValue;
            for (int offset = 0; offset < count; offset++)
            {
                int frame = ring[(head + offset) % ring.Length]!.frameIndex;
                if (frame < oldest)
                {
                    oldest = frame;
                }
            }
            return oldest;
        }
    }

    public int newestFrame
    {
        get
        {
            if (count == 0)
            {
                return -1;
            }
            int newest = -1;
            for (int offset = 0; offset < count; offset++)
            {
                int frame = ring[(head + offset) % ring.Length]!.frameIndex;
                if (frame > newest)
                {
                    newest = frame;
                }
            }
            return newest;
        }
    }

    public void clear()
    {
        Array.Clear(ring, 0, ring.Length);
        head = 0;
        count = 0;
        memoryBytes = 0;
    }
}
