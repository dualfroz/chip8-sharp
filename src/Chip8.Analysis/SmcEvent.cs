using System.Globalization;

namespace Chip8.Analysis;

public sealed class SmcEvent
{
    public SmcEvent(int step, ushort writerAddress, ushort targetAddress, byte oldValue, byte newValue)
    {
        this.step = step;
        this.writerAddress = writerAddress;
        this.targetAddress = targetAddress;
        this.oldValue = oldValue;
        this.newValue = newValue;
    }

    public int step { get; }
    public ushort writerAddress { get; }
    public ushort targetAddress { get; }
    public byte oldValue { get; }
    public byte newValue { get; }

    public override string ToString()
    {
        return "step " + step.ToString(CultureInfo.InvariantCulture)
            + ": writer 0x" + writerAddress.ToString("X4", CultureInfo.InvariantCulture)
            + " changed 0x" + targetAddress.ToString("X4", CultureInfo.InvariantCulture)
            + " from 0x" + oldValue.ToString("X2", CultureInfo.InvariantCulture)
            + " to 0x" + newValue.ToString("X2", CultureInfo.InvariantCulture);
    }
}
