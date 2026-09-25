using System;
using System.IO;
using System.Text;

namespace Chip8.Core.Export;

public static class PpmImage
{
    public static byte[] fromMonochrome(bool[] pixels, int width, int height, Palette palette, int scale = 1) =>
        fromFrame(FrameImage.fromMonochrome(pixels, width, height, palette), scale);

    public static byte[] fromVideo(Video video, Palette palette, int scale = 1) =>
        fromFrame(FrameImage.fromVideo(video, palette), scale);

    public static byte[] fromMachine(Machine machine, Palette palette, int scale = 1) =>
        fromFrame(FrameImage.fromMachine(machine, palette), scale);

    public static void writeFile(string path, byte[] ppm) => File.WriteAllBytes(path, ppm);

    public static byte[] fromFrame(FrameImage frame, int scale = 1)
    {
        if (scale < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "scale must be at least 1");
        }

        int outWidth = frame.width * scale;
        int outHeight = frame.height * scale;
        byte[] header = Encoding.ASCII.GetBytes("P6\n" + outWidth + " " + outHeight + "\n255\n");

        var output = new byte[header.Length + outWidth * outHeight * 3];
        Array.Copy(header, output, header.Length);

        int cursor = header.Length;
        int source = 0;
        for (int y = 0; y < frame.height; y++)
        {
            int rowStart = cursor;
            for (int x = 0; x < frame.width; x++)
            {
                for (int sx = 0; sx < scale; sx++)
                {
                    output[cursor++] = frame.rgba[source];
                    output[cursor++] = frame.rgba[source + 1];
                    output[cursor++] = frame.rgba[source + 2];
                }
                source += FrameImage.bytesPerPixel;
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
