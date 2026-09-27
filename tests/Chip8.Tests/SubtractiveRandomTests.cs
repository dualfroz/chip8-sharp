using System;
using System.IO;
using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class SubtractiveRandomTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(-7)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void seededSequenceMatchesSystemRandom(int seed)
    {
        var reference = new Random(seed);
        var generator = new SubtractiveRandom(seed);

        for (int draw = 0; draw < 5000; draw++)
        {
            Assert.Equal(reference.Next(256), generator.nextBelow(256));
        }
    }

    [Fact]
    public void serializedStateContinuesTheSequence()
    {
        var original = new SubtractiveRandom(1234);
        for (int draw = 0; draw < 300; draw++)
        {
            original.nextBelow(256);
        }

        using var stream = new MemoryStream();
        original.writeTo(new BinaryWriter(stream));
        Assert.Equal(SubtractiveRandom.stateSize, stream.Length);

        stream.Position = 0;
        var copy = new SubtractiveRandom(99);
        copy.readFrom(new BinaryReader(stream));

        for (int draw = 0; draw < 1000; draw++)
        {
            Assert.Equal(original.nextBelow(256), copy.nextBelow(256));
        }
    }
}
