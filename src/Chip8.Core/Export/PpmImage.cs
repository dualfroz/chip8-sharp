using System;
using System.IO;
using System.Text;

namespace Chip8.Core.Export;

public static class PpmImage
{
    public static byte[] fromMonochrome(bool[] pixels, int width, int height, Palette palette, int scale = 1)
    {
        if (pixels.Length < width * height)
        {
            throw new ArgumentException("pixel buffer is smaller than " + width + "x" + height, nameof(pixels));
        }
        return build(width, height, scale, palette, (x, y) => pixels[y * width + x] ? 1 : 0);
    }

    public static byte[] fromVideo(Video video, Palette palette, int scale = 1) =>
        build(video.width, video.height, scale, palette, video.colorAt);

    public static byte[] fromMachine(Machine machine, Palette palette, int scale = 1)
    {
        if (machine.platform == Platform.Chip8)
        {
            return fromMonochrome(machine.display, Machine.displayWidth, Machine.displayHeight, palette, scale);
        }
        return fromVideo(machine.video, palette, scale);
    }

    public static void writeFile(string path, byte[] ppm) => File.WriteAllBytes(path, ppm);

    static byte[] build(int width, int height, int scale, Palette palette, Func<int, int, int> colorAt)
    {
        if (scale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "scale must be at least 1");
        }

        int outWidth = width * scale;
        int outHeight = height * scale;
        byte[] header = Encoding.ASCII.GetBytes("P6\n" + outWidth + " " + outHeight + "\n255\n");

        var output = new byte[header.Length + outWidth * outHeight * 3];
        Array.Copy(header, output, header.Length);

        int cursor = header.Length;
        for (int y = 0; y < height; y++)
        {
            int rowStart = cursor;
            for (int x = 0; x < width; x++)
            {
                Rgb color = palette.resolve(colorAt(x, y));
                for (int sx = 0; sx < scale; sx++)
                {
                    output[cursor++] = color.r;
                    output[cursor++] = color.g;
                    output[cursor++] = color.b;
                }
            }

            int rowBytes = outWidth * 3;
            for (int sy = 1; sy < scale; sy++)
            {
                Array.Copy(output, rowStart, output, cursor, rowBytes);
                cursor += rowBytes;
            }
        }

        return output;
    }
}
