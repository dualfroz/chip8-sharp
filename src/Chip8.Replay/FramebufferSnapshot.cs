using System;
using Chip8.Core;

namespace Chip8.Replay;

public sealed class FramebufferSnapshot
{
    public int width { get; }
    public int height { get; }
    public byte[] colors { get; }

    FramebufferSnapshot(int width, int height, byte[] colors)
    {
        this.width = width;
        this.height = height;
        this.colors = colors;
    }

    public int pixelCount => width * height;

    public byte colorAt(int x, int y) => colors[y * width + x];

    public bool isLit(int x, int y) => colors[y * width + x] != 0;

    public int litCount
    {
        get
        {
            int lit = 0;
            foreach (byte color in colors)
            {
                if (color != 0)
                {
                    lit++;
                }
            }
            return lit;
        }
    }

    public static FramebufferSnapshot capture(Machine machine)
    {
        if (machine.platform == Platform.Chip8)
        {
            return captureBaseDisplay(machine);
        }
        return captureVideo(machine.video);
    }

    static FramebufferSnapshot captureBaseDisplay(Machine machine)
    {
        int width = Machine.displayWidth;
        int height = Machine.displayHeight;
        var colors = new byte[width * height];
        for (int index = 0; index < colors.Length; index++)
        {
            colors[index] = machine.display[index] ? (byte)1 : (byte)0;
        }
        return new FramebufferSnapshot(width, height, colors);
    }

    static FramebufferSnapshot captureVideo(Video video)
    {
        int width = video.width;
        int height = video.height;
        var colors = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colors[y * width + x] = (byte)video.colorAt(x, y);
            }
        }
        return new FramebufferSnapshot(width, height, colors);
    }

    public bool sameAs(FramebufferSnapshot other)
    {
        if (width != other.width || height != other.height || colors.Length != other.colors.Length)
        {
            return false;
        }

        for (int index = 0; index < colors.Length; index++)
        {
            if (colors[index] != other.colors[index])
            {
                return false;
            }
        }

        return true;
    }

    public string toAscii(string palette = " #*@")
    {
        var builder = new System.Text.StringBuilder((width + 1) * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int color = colors[y * width + x];
                builder.Append(color < palette.Length ? palette[color] : palette[palette.Length - 1]);
            }
            builder.Append('\n');
        }
        return builder.ToString();
    }
}
