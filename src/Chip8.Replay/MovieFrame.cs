using System;

namespace Chip8.Replay;

public readonly struct MovieFrame : IEquatable<MovieFrame>
{
    public int index { get; }
    public KeyMask keys { get; }

    public MovieFrame(int index, KeyMask keys)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "movie frame index cannot be negative");
        }
        this.index = index;
        this.keys = keys;
    }

    public MovieFrame(int index, ushort keyBits)
        : this(index, new KeyMask(keyBits))
    {
    }

    public bool Equals(MovieFrame other) => index == other.index && keys == other.keys;

    public override bool Equals(object? obj) => obj is MovieFrame other && Equals(other);

    public override int GetHashCode() => (index * 397) ^ keys.bits;

    public override string ToString() => "frame " + index + " " + keys.toKeyList();
}
