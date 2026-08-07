namespace Chip8.Replay;

public sealed class TimelineSnapshot
{
    public int frameIndex { get; }
    public byte[] state { get; }

    public TimelineSnapshot(int frameIndex, byte[] state)
    {
        this.frameIndex = frameIndex;
        this.state = state;
    }

    public int stateSize => state.Length;

    public override string ToString() => "snapshot frame=" + frameIndex + " bytes=" + state.Length;
}
