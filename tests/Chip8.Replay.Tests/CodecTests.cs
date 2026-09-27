using System;
using System.Text;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class CodecTests
{
    [Fact]
    public void keyMaskTracksPressedKeys()
    {
        KeyMask mask = KeyMask.fromKeys(1, 5, 0xA);

        Assert.Equal(3, mask.pressedCount);
        Assert.True(mask.isPressed(5));
        Assert.False(mask.isPressed(4));
        Assert.Equal("1,5,A", mask.toKeyList());
        Assert.Equal("0422", mask.toHex());
        Assert.Equal(mask, KeyMask.parseKeyList("1,5,A"));
        Assert.Equal(mask, KeyMask.parseHex("0422"));
        Assert.Equal(KeyMask.fromKeys(1, 0xA), mask.withReleased(5));
        Assert.Equal(KeyMask.fromKeys(1, 5, 0xA, 0xF), mask.with(0xF, true));
        Assert.Equal(KeyMask.fromKeys(1, 5, 0xA, 2), mask.union(KeyMask.fromKeys(2)));
        Assert.Equal(KeyMask.fromKeys(5), mask.intersect(KeyMask.fromKeys(5, 6)));
        Assert.True(mask != KeyMask.empty);
    }

    [Fact]
    public void emptyKeyMaskUsesDash()
    {
        Assert.True(KeyMask.empty.isEmpty);
        Assert.Equal("-", KeyMask.empty.toKeyList());
        Assert.Equal(KeyMask.empty, KeyMask.parseKeyList("-"));
        Assert.Equal(KeyMask.empty, KeyMask.parseKeyList(""));
    }

    [Fact]
    public void keyMaskRoundTripsThroughTheKeypad()
    {
        var keypad = new bool[16];
        KeyMask.fromKeys(0, 7, 0xF).applyTo(keypad);

        Assert.True(keypad[0] && keypad[7] && keypad[15]);
        Assert.False(keypad[1]);
        Assert.Equal(KeyMask.fromKeys(0, 7, 0xF), KeyMask.fromKeypad(keypad));

        KeyMask.empty.applyTo(keypad);
        Assert.DoesNotContain(true, keypad);
    }

    [Theory]
    [InlineData("modern", "clip")]
    [InlineData("cosmac", "shiftVy+incI+resetVf+clip")]
    [InlineData("superchip", "jumpVx+clip")]
    [InlineData("xochip", "none")]
    public void quirkPresetsRoundTripThroughTokensAndBytes(string preset, string tokens)
    {
        Quirks quirks = preset switch
        {
            "cosmac" => Quirks.cosmacVip(),
            "superchip" => Quirks.superChip(),
            "xochip" => Quirks.xoChip(),
            _ => Quirks.modern(),
        };

        Assert.Equal(tokens, QuirksCodec.toTokens(quirks));
        Assert.True(QuirksCodec.sameFlags(quirks, QuirksCodec.fromTokens(tokens)));
        Assert.True(QuirksCodec.sameFlags(quirks, QuirksCodec.fromByte(QuirksCodec.toByte(quirks))));
    }

    [Fact]
    public void quirkTokensAreCaseInsensitiveAndValidated()
    {
        Quirks parsed = QuirksCodec.fromTokens("CLIP+carryi");
        Assert.True(parsed.clipSprites);
        Assert.True(parsed.addToIndexSetsCarry);
        Assert.False(parsed.shiftUsesVy);

        var error = Assert.Throws<FormatException>(() => QuirksCodec.fromTokens("clip+wobble"));
        Assert.Contains("wobble", error.Message);
    }

    [Fact]
    public void quirkCloneIsIndependent()
    {
        Quirks original = Quirks.modern();
        Quirks copy = QuirksCodec.clone(original);
        copy.shiftUsesVy = true;

        Assert.False(original.shiftUsesVy);
        Assert.False(QuirksCodec.sameFlags(original, copy));
    }

    [Theory]
    [InlineData("", "CBF29CE484222325")]
    [InlineData("a", "AF63DC4C8601EC8C")]
    [InlineData("foobar", "85944171F73967E8")]
    public void romHashIsFnv1a64(string text, string expected)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);

        Assert.Equal(expected, RomHash.toHex(bytes));
        Assert.Equal(RomHash.compute(bytes), RomHash.parse(expected));
    }

    [Fact]
    public void framebufferHashFollowsPixelsAndRegisters()
    {
        var first = TestPrograms.machineFor(TestPrograms.keyMirror);
        var second = TestPrograms.machineFor(TestPrograms.keyMirror);
        first.runFrame(TestPrograms.instructionsPerFrame);
        second.runFrame(TestPrograms.instructionsPerFrame);

        Assert.Equal(FramebufferHash.compute(first), FramebufferHash.compute(second));
        Assert.Equal(FramebufferHash.computeWithRegisters(first), FramebufferHash.computeWithRegisters(second));

        second.v[7] = 0x99;
        Assert.Equal(FramebufferHash.compute(first), FramebufferHash.compute(second));
        Assert.NotEqual(FramebufferHash.computeWithRegisters(first), FramebufferHash.computeWithRegisters(second));

        second.display[100] = !second.display[100];
        Assert.NotEqual(FramebufferHash.compute(first), FramebufferHash.compute(second));
        Assert.Equal(16, FramebufferHash.toHex(first).Length);
    }

    [Fact]
    public void snapshotCapturesTheBaseDisplay()
    {
        var machine = TestPrograms.machineFor(TestPrograms.keyMirror);
        machine.runFrame(TestPrograms.instructionsPerFrame);
        FramebufferSnapshot snapshot = FramebufferSnapshot.capture(machine);

        Assert.Equal(64, snapshot.width);
        Assert.Equal(32, snapshot.height);
        Assert.Equal(14, snapshot.litCount);
        Assert.True(snapshot.isLit(0, 0));
        Assert.False(snapshot.isLit(4, 0));
        Assert.StartsWith("####    ", snapshot.toAscii());
        Assert.True(snapshot.sameAs(FramebufferSnapshot.capture(machine)));
    }

    [Fact]
    public void snapshotCapturesHighResolutionVideo()
    {
        var machine = TestPrograms.machineFor(TestPrograms.exitsAfterDrawing, Platform.SuperChip);
        for (int step = 0; step < 10 && !machine.exited; step++)
        {
            machine.step();
        }
        FramebufferSnapshot snapshot = FramebufferSnapshot.capture(machine);

        Assert.True(machine.exited);
        Assert.Equal(128, snapshot.width);
        Assert.Equal(64, snapshot.height);
        Assert.Equal(16, snapshot.litCount);
        Assert.True(snapshot.isLit(100, 40));
        Assert.True(snapshot.isLit(107, 41));
        Assert.False(snapshot.isLit(108, 40));
        Assert.Equal(1, snapshot.colorAt(100, 41));
    }

    [Fact]
    public void benchmarkResultFormatsRates()
    {
        var benchmark = new BenchmarkResult("x.ch8", 1_000_000, 1000, 500, 0, false);

        Assert.Equal(2_000_000, benchmark.instructionsPerSecond, 6);
        Assert.Equal(2000, benchmark.framesPerSecond, 6);
        Assert.Equal("x.ch8: 1000000 steps / 1000 frames in 500.00 ms (2.00 Mips, 2000 fps)", benchmark.format());

        var instant = new BenchmarkResult("y.ch8", 10, 1, 0, 0, true);
        Assert.True(double.IsNaN(instant.instructionsPerSecond));
        Assert.Equal("y.ch8: 10 steps / 1 frames in 0.00 ms", instant.ToString());
    }
}
