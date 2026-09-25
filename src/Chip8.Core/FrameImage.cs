using System;

namespace Chip8.Core;

public sealed class FrameImage
{
    public const int bytesPerPixel = 4;

    FrameImage(int width, int height, byte[] rgba)
    {
        this.width = width;
        this.height = height;
        this.rgba = rgba;
    }

    public int width { get; }
    public int height { get; }
    public byte[] rgba { get; }

    public Rgb colorAt(int x, int y)
    {
        int offset = (y * width + x) * bytesPerPixel;
        return new Rgb(rgba[offset], rgba[offset + 1], rgba[offset + 2]);
    }

    public static FrameImage fromMachine(Machine machine, Palette palette)
    {
        if (machine.platform == Platform.Chip8)
        {
            return fromMonochrome(machine.display, Machine.displayWidth, Machine.displayHeight, palette);
        }
        return fromVideo(machine.video, palette);
    }

    public static FrameImage fromVideo(Video video, Palette palette) =>
        build(video.width, video.height, palette, video.colorAt);

    public static FrameImage fromMonochrome(bool[] pixels, int width, int height, Palette palette)
    {
        if (pixels.Length < width * height)
        {
            throw new ArgumentException("pixel buffer is smaller than " + width + "x" + height, nameof(pixels));
        }
        return build(width, height, palette, (x, y) => pixels[y * width + x] ? 1 : 0);
    }

    static FrameImage build(int width, int height, Palette palette, Func<int, int, int> colorIndexAt)
    {
        var rgba = new byte[width * height * bytesPerPixel];
        int cursor = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgb color = palette.resolve(colorIndexAt(x, y));
                rgba[cursor++] = color.r;
                rgba[cursor++] = color.g;
                rgba[cursor++] = color.b;
                rgba[cursor++] = 0xFF;
            }
        }
        return new FrameImage(width, height, rgba);
    }
}
