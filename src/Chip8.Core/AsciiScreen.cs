using System.Text;

namespace Chip8.Core;

public static class AsciiScreen
{
    public static string render(bool[] display, char on = '#', char off = ' ')
    {
        var builder = new StringBuilder((Machine.displayWidth + 1) * Machine.displayHeight);

        for (int row = 0; row < Machine.displayHeight; row++)
        {
            for (int col = 0; col < Machine.displayWidth; col++)
            {
                builder.Append(display[row * Machine.displayWidth + col] ? on : off);
            }
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static string renderFramed(bool[] display, char on = '#', char off = ' ')
    {
        var builder = new StringBuilder();
        string border = new string('-', Machine.displayWidth);

        builder.Append('+').Append(border).Append("+\n");
        for (int row = 0; row < Machine.displayHeight; row++)
        {
            builder.Append('|');
            for (int col = 0; col < Machine.displayWidth; col++)
            {
                builder.Append(display[row * Machine.displayWidth + col] ? on : off);
            }
            builder.Append("|\n");
        }
        builder.Append('+').Append(border).Append("+\n");

        return builder.ToString();
    }

    public static int litPixelCount(bool[] display)
    {
        int lit = 0;
        foreach (bool pixel in display)
        {
            if (pixel)
            {
                lit++;
            }
        }
        return lit;
    }

    public static string render(Video video, string palette = " #*@")
    {
        var builder = new StringBuilder((video.width + 1) * video.height);

        for (int row = 0; row < video.height; row++)
        {
            for (int col = 0; col < video.width; col++)
            {
                builder.Append(palette[video.colorAt(col, row)]);
            }
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static int litPixelCount(Video video)
    {
        int lit = 0;
        for (int row = 0; row < video.height; row++)
        {
            for (int col = 0; col < video.width; col++)
            {
                if (video.colorAt(col, row) != 0)
                {
                    lit++;
                }
            }
        }
        return lit;
    }
}
