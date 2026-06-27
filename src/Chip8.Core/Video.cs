using System.IO;

namespace Chip8.Core;

public sealed class Video
{
    public const int loWidth = 64;
    public const int loHeight = 32;
    public const int hiWidth = 128;
    public const int hiHeight = 64;
    public const int maxPixels = hiWidth * hiHeight;
    public const int planeCount = 2;
    public const int scrollStep = 4;

    public int width { get; private set; } = loWidth;
    public int height { get; private set; } = loHeight;
    public bool hires => width == hiWidth;

    readonly bool[][] plane =
    {
        new bool[maxPixels],
        new bool[maxPixels],
    };

    public void reset()
    {
        width = loWidth;
        height = loHeight;
        foreach (var buffer in plane)
        {
            System.Array.Clear(buffer, 0, buffer.Length);
        }
    }

    public void setResolution(bool high)
    {
        width = high ? hiWidth : loWidth;
        height = high ? hiHeight : loHeight;
        foreach (var buffer in plane)
        {
            System.Array.Clear(buffer, 0, buffer.Length);
        }
    }

    public bool pixel(int planeIndex, int x, int y) => plane[planeIndex][y * width + x];

    internal void writeTo(BinaryWriter writer)
    {
        writer.Write(width);
        writer.Write(height);
        for (int p = 0; p < planeCount; p++)
        {
            for (int cell = 0; cell < maxPixels; cell++)
            {
                writer.Write(plane[p][cell]);
            }
        }
    }

    internal void readFrom(BinaryReader reader)
    {
        width = reader.ReadInt32();
        height = reader.ReadInt32();
        for (int p = 0; p < planeCount; p++)
        {
            for (int cell = 0; cell < maxPixels; cell++)
            {
                plane[p][cell] = reader.ReadBoolean();
            }
        }
    }

    public int colorAt(int x, int y)
    {
        int index = y * width + x;
        int color = 0;
        for (int p = 0; p < planeCount; p++)
        {
            if (plane[p][index])
            {
                color |= 1 << p;
            }
        }
        return color;
    }

    public void clear(int planeMask)
    {
        for (int p = 0; p < planeCount; p++)
        {
            if ((planeMask & (1 << p)) == 0)
            {
                continue;
            }
            for (int cell = 0; cell < width * height; cell++)
            {
                plane[p][cell] = false;
            }
        }
    }

    public void scrollDown(int rows, int planeMask) => shift(0, rows, planeMask);

    public void scrollUp(int rows, int planeMask) => shift(0, -rows, planeMask);

    public void scrollRight(int columns, int planeMask) => shift(columns, 0, planeMask);

    public void scrollLeft(int columns, int planeMask) => shift(-columns, 0, planeMask);

    void shift(int dx, int dy, int planeMask)
    {
        for (int p = 0; p < planeCount; p++)
        {
            if ((planeMask & (1 << p)) == 0)
            {
                continue;
            }

            bool[] buffer = plane[p];
            var source = (bool[])buffer.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = x - dx;
                    int sy = y - dy;
                    bool value = sx >= 0 && sx < width && sy >= 0 && sy < height
                        && source[sy * width + sx];
                    buffer[y * width + x] = value;
                }
            }
        }
    }

    public bool drawSprite(byte[] memory, int address, int originX, int originY, int rows, int planeMask, bool wrap)
    {
        int spriteRows = rows == 0 ? 16 : rows;
        int bytesPerRow = rows == 0 ? 2 : 1;
        int spriteWidth = bytesPerRow * 8;
        int startX = originX % width;
        int startY = originY % height;
        bool collision = false;
        int addr = address;

        for (int p = 0; p < planeCount; p++)
        {
            if ((planeMask & (1 << p)) == 0)
            {
                continue;
            }

            bool[] buffer = plane[p];
            for (int row = 0; row < spriteRows; row++)
            {
                int rowBits = 0;
                for (int b = 0; b < bytesPerRow; b++)
                {
                    rowBits = (rowBits << 8) | memory[addr++ & (memory.Length - 1)];
                }

                for (int col = 0; col < spriteWidth; col++)
                {
                    if ((rowBits & (1 << (spriteWidth - 1 - col))) == 0)
                    {
                        continue;
                    }

                    int px = startX + col;
                    int py = startY + row;
                    if (wrap)
                    {
                        px %= width;
                        py %= height;
                    }
                    else if (px >= width || py >= height)
                    {
                        continue;
                    }

                    int index = py * width + px;
                    if (buffer[index])
                    {
                        collision = true;
                    }
                    buffer[index] ^= true;
                }
            }
        }

        return collision;
    }
}
