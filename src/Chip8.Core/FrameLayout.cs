using System;

namespace Chip8.Core;

public readonly struct FrameLayout
{
    public FrameLayout(int x, int y, int pixelSize, int width, int height)
    {
        this.x = x;
        this.y = y;
        this.pixelSize = pixelSize;
        this.width = width;
        this.height = height;
    }

    public int x { get; }
    public int y { get; }
    public int pixelSize { get; }
    public int width { get; }
    public int height { get; }

    public static FrameLayout fit(int frameWidth, int frameHeight, int viewWidth, int viewHeight)
    {
        int pixelSize = Math.Max(1, Math.Min(viewWidth / frameWidth, viewHeight / frameHeight));
        int width = frameWidth * pixelSize;
        int height = frameHeight * pixelSize;
        return new FrameLayout((viewWidth - width) / 2, (viewHeight - height) / 2, pixelSize, width, height);
    }

    public static (int width, int height) windowSize(Platform platform, int scale)
    {
        if (platform == Platform.Chip8)
        {
            return (Video.loWidth * scale, Video.loHeight * scale);
        }
        int hiresPixel = (scale + 1) / 2;
        return (Video.hiWidth * hiresPixel, Video.hiHeight * hiresPixel);
    }
}
