using System;
using System.IO;
using System.Linq;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class InputMovieTests
{
    static InputMovie sampleMovie()
    {
        var movie = new InputMovie
        {
            platform = Platform.SuperChip,
            quirks = Quirks.superChip(),
            randomSeed = 42,
            instructionsPerFrame = 20,
        };
        movie.setRom(new byte[] { 0x00, 0xE0 }, "clear.ch8");
        movie.setFrame(3, KeyMask.fromKeys(5));
        movie.setFrame(4, KeyMask.fromKeys(5, 6));
        movie.extendLength(10);
        return movie;
    }

    [Fact]
    public void textFormatRoundTrips()
    {
        InputMovie movie = sampleMovie();
        string text = movie.toText();
        InputMovie parsed = InputMovie.parseText(text);

        Assert.StartsWith("CHIP8MOVIE 1\nplatform SuperChip\nquirks jumpVx+clip\nseed 42\nipf 20\nlength 10\n", text);
        Assert.Contains("frame 4 5,6\n", text);
        Assert.True(movie.sameContent(parsed));
        Assert.Equal(10, parsed.length);
        Assert.Equal(KeyMask.fromKeys(5, 6), parsed.keysAt(4));
        Assert.Equal(KeyMask.empty, parsed.keysAt(5));
    }

    [Fact]
    public void binaryFormatRoundTrips()
    {
        InputMovie movie = sampleMovie();
        byte[] bytes = movie.toBytes();
        InputMovie parsed = InputMovie.parseBytes(bytes);

        Assert.Equal((byte)'C', bytes[0]);
        Assert.Equal((byte)'V', bytes[3]);
        Assert.True(movie.sameContent(parsed));
        Assert.Equal(new[] { 3, 4 }, parsed.entries().Select(frame => frame.index).ToArray());
    }

    [Fact]
    public void textParserIgnoresCommentsAndAcceptsMissingSeed()
    {
        InputMovie movie = InputMovie.parseText(
            "# recorded by hand\nCHIP8MOVIE 1\n\nplatform chip8\nquirks none\nseed none\nipf 11\nlength 3\nrom demo.ch8 00000000000000FF\nframe 1 A\n");

        Assert.Equal(Platform.Chip8, movie.platform);
        Assert.Null(movie.randomSeed);
        Assert.Equal(3, movie.length);
        Assert.Equal("demo.ch8", movie.romName);
        Assert.Equal(0xFFUL, movie.romHash);
        Assert.Equal(KeyMask.fromKeys(0xA), movie.keysAt(1));
    }

    [Theory]
    [InlineData("platform chip8\n", "missing 'CHIP8MOVIE' header")]
    [InlineData("CHIP8MOVIE 2\n", "unsupported movie version")]
    [InlineData("CHIP8MOVIE 1\ntempo 3\n", "unknown movie directive 'tempo'")]
    [InlineData("", "empty movie text")]
    public void textParserRejectsMalformedMovies(string text, string message)
    {
        var error = Assert.Throws<FormatException>(() => InputMovie.parseText(text));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void binaryParserRejectsBadMagicAndVersion()
    {
        Assert.Throws<InvalidDataException>(() => InputMovie.parseBytes(new byte[] { 1, 2, 3, 4, 1 }));

        byte[] bytes = sampleMovie().toBytes();
        bytes[4] = 9;
        var error = Assert.Throws<InvalidDataException>(() => InputMovie.parseBytes(bytes));
        Assert.Contains("version 9", error.Message);
    }

    [Fact]
    public void emptyKeysAreNotStoredButStillExtendTheLength()
    {
        var movie = new InputMovie();
        movie.setFrame(2, KeyMask.fromKeys(1));
        movie.setFrame(2, KeyMask.empty);
        movie.recordFrame(7, KeyMask.empty);

        Assert.Equal(0, movie.entryCount);
        Assert.Equal(8, movie.length);
        Assert.Throws<ArgumentOutOfRangeException>(() => movie.setFrame(-1, KeyMask.fromKeys(1)));
    }

    [Fact]
    public void cloneIsIndependentAndRomMatchingUsesTheHash()
    {
        InputMovie movie = sampleMovie();
        InputMovie copy = movie.clone();
        copy.setFrame(9, KeyMask.fromKeys(0));

        Assert.False(movie.sameContent(copy));
        Assert.Equal(KeyMask.empty, movie.keysAt(9));
        Assert.True(movie.matchesRom(new byte[] { 0x00, 0xE0 }));
        Assert.False(movie.matchesRom(new byte[] { 0x00, 0xEE }));
    }

    [Fact]
    public void machineBuilderUsesTheMovieHeader()
    {
        InputMovie movie = sampleMovie();
        Machine machine = MachineBuilder.fromMovie(movie, new byte[] { 0x00, 0xE0 });

        Assert.Equal(Platform.SuperChip, machine.platform);
        Assert.True(machine.quirks.jumpWithVx);
        Assert.Equal(0x00, machine.memory[0x200]);
        Assert.Equal(0xE0, machine.memory[0x201]);

        InputMovie header = MachineBuilder.headerFor(machine, new byte[] { 0x00, 0xE0 }, "clear.ch8", 20, 42);
        Assert.Equal(movie.romHash, header.romHash);
        Assert.Equal(42, header.randomSeed);
        Assert.True(QuirksCodec.sameFlags(movie.quirks, header.quirks));
        Assert.NotSame(machine.quirks, header.quirks);
    }

    [Fact]
    public void movieFrameRejectsNegativeIndices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovieFrame(-1, KeyMask.empty));
        Assert.Equal("frame 3 1,2", new MovieFrame(3, 0x0006).ToString());
    }
}
