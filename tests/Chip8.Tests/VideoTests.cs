using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class VideoTests
{
    static Video withPixel(int x, int y, int planeMask = 1)
    {
        var video = new Video();
        var sprite = new byte[] { 0x80 };
        video.drawSprite(sprite, 0, x, y, 1, planeMask, false);
        return video;
    }

    [Fact]
    public void scrollDownMovesRowsDown()
    {
        var video = withPixel(5, 2);
        video.scrollDown(3, 1);
        Assert.False(video.pixel(0, 5, 2));
        Assert.True(video.pixel(0, 5, 5));
    }

    [Fact]
    public void scrollUpMovesRowsUp()
    {
        var video = withPixel(5, 10);
        video.scrollUp(3, 1);
        Assert.False(video.pixel(0, 5, 10));
        Assert.True(video.pixel(0, 5, 7));
    }

    [Fact]
    public void scrollRightMovesColumnsRight()
    {
        var video = withPixel(5, 2);
        video.scrollRight(4, 1);
        Assert.False(video.pixel(0, 5, 2));
        Assert.True(video.pixel(0, 9, 2));
    }

    [Fact]
    public void scrollLeftMovesColumnsLeft()
    {
        var video = withPixel(9, 2);
        video.scrollLeft(4, 1);
        Assert.False(video.pixel(0, 9, 2));
        Assert.True(video.pixel(0, 5, 2));
    }

    [Fact]
    public void scrollRespectsPlaneMask()
    {
        var video = new Video();
        var sprite = new byte[] { 0x80 };
        video.drawSprite(sprite, 0, 5, 2, 1, 0x3, false);
        video.scrollDown(3, 0x1);
        Assert.True(video.pixel(0, 5, 5));
        Assert.True(video.pixel(1, 5, 2));
        Assert.False(video.pixel(1, 5, 5));
    }

    [Fact]
    public void setResolutionHiresResizesAndClears()
    {
        var video = withPixel(5, 2);
        video.setResolution(true);
        Assert.Equal(Video.hiWidth, video.width);
        Assert.Equal(Video.hiHeight, video.height);
        Assert.False(video.pixel(0, 5, 2));
    }

    [Fact]
    public void drawSpriteWrapsAroundEdgesWhenNotClipping()
    {
        var video = new Video();
        var sprite = new byte[] { 0xC0 };
        video.drawSprite(sprite, 0, 63, 0, 1, 1, true);
        Assert.True(video.pixel(0, 63, 0));
        Assert.True(video.pixel(0, 0, 0));
    }

    [Fact]
    public void drawSpriteClipsAtEdgeWhenClipping()
    {
        var video = new Video();
        var sprite = new byte[] { 0xC0 };
        video.drawSprite(sprite, 0, 63, 0, 1, 1, false);
        Assert.True(video.pixel(0, 63, 0));
        Assert.False(video.pixel(0, 0, 0));
    }

    [Fact]
    public void sixteenBySixteenSpriteFillsBlock()
    {
        var video = new Video();
        var sprite = new byte[32];
        for (int index = 0; index < sprite.Length; index++)
        {
            sprite[index] = 0xFF;
        }
        bool collision = video.drawSprite(sprite, 0, 0, 0, 0, 1, false);
        Assert.False(collision);
        Assert.Equal(256, AsciiScreen.litPixelCount(video));
    }
}
