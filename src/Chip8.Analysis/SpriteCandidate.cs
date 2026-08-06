using System.Globalization;
using System.Text;

namespace Chip8.Analysis;

public sealed class SpriteCandidate
{
    public SpriteCandidate(ushort address, int rows, int bytesPerRow, byte[] bytes, ushort loadAddress, ushort drawAddress)
    {
        this.address = address;
        this.rows = rows;
        this.bytesPerRow = bytesPerRow;
        this.bytes = bytes;
        this.loadAddress = loadAddress;
        this.drawAddress = drawAddress;
    }

    public ushort address { get; }
    public int rows { get; }
    public int bytesPerRow { get; }
    public byte[] bytes { get; }
    public ushort loadAddress { get; }
    public ushort drawAddress { get; }

    public int widthPixels => bytesPerRow * 8;

    public int setPixelCount
    {
        get
        {
            int count = 0;
            foreach (byte value in bytes)
            {
                int bits = value;
                while (bits != 0)
                {
                    count += bits & 1;
                    bits >>= 1;
                }
            }
            return count;
        }
    }

    public bool isBlank => setPixelCount == 0;

    public bool pixel(int row, int column)
    {
        int byteIndex = row * bytesPerRow + column / 8;
        if (byteIndex >= bytes.Length)
        {
            return false;
        }
        int bit = 7 - column % 8;
        return (bytes[byteIndex] & (1 << bit)) != 0;
    }

    public string renderAscii(char on = '#', char off = '.')
    {
        var builder = new StringBuilder();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < widthPixels; column++)
            {
                builder.Append(pixel(row, column) ? on : off);
            }
            builder.Append('\n');
        }
        return builder.ToString();
    }

    public override string ToString() =>
        "sprite@0x" + address.ToString("X4", CultureInfo.InvariantCulture)
        + " " + widthPixels + "x" + rows
        + " (" + setPixelCount + " px, drawn at 0x" + drawAddress.ToString("X4", CultureInfo.InvariantCulture) + ")";
}
