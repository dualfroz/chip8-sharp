using System;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class TimelineTests
{
    static InputMovie mirrorMovie()
    {
        InputMovie movie = TestPrograms.movieFor(TestPrograms.keyMirror, Platform.Chip8, 11);
        for (int frame = 10; frame < 25; frame++)
        {
            movie.setFrame(frame, KeyMask.fromKeys(5));
        }
        movie.extendLength(60);
        return movie;
    }

    static byte[] stateAfterFreshReplay(InputMovie movie, int frames)
    {
        ReplayEngine engine = ReplayEngine.playback(movie, TestPrograms.keyMirror);
        engine.advanceFrames(frames);
        return engine.machine.saveState();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(39)]
    public void rewindingMatchesAFreshReplay(int target)
    {
        InputMovie movie = mirrorMovie();
        Timeline timeline = Timeline.forMovie(movie, TestPrograms.keyMirror, keyframeInterval: 8);

        timeline.advanceTo(40);
        timeline.rewindTo(target);

        Assert.Equal(target, timeline.currentFrame);
        Assert.Equal(stateAfterFreshReplay(movie, target), timeline.currentState());
        Assert.NotEqual(stateAfterFreshReplay(movie, 40), timeline.currentState());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(37)]
    public void rewindingARandomRomReplaysTheSameFrames(int target)
    {
        InputMovie movie = TestPrograms.movieFor(TestPrograms.randomWalker, Platform.XoChip, 9);
        movie.extendLength(60);
        Timeline timeline = Timeline.forMovie(movie, TestPrograms.randomWalker, keyframeInterval: 8);

        timeline.advanceTo(50);
        ReplayOutcome expected = timeline.outcome();
        byte[] expectedState = timeline.currentState();

        timeline.rewindTo(target);
        timeline.advanceTo(50);

        Assert.True(expected.sameAs(timeline.outcome()));
        Assert.Equal(expectedState, timeline.currentState());
    }

    [Fact]
    public void stepBackAndForwardStayConsistent()
    {
        InputMovie movie = mirrorMovie();
        Timeline timeline = Timeline.forMovie(movie, TestPrograms.keyMirror, keyframeInterval: 4);

        Assert.False(timeline.stepBack());
        timeline.advanceFrames(20);
        ReplayOutcome at20 = timeline.outcome();

        Assert.True(timeline.stepBack());
        Assert.Equal(19, timeline.currentFrame);
        timeline.advanceFrame();
        Assert.True(at20.sameAs(timeline.outcome()));
    }

    [Fact]
    public void timelineGuardsItsDirection()
    {
        Timeline timeline = Timeline.forMovie(mirrorMovie(), TestPrograms.keyMirror);
        timeline.advanceTo(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.advanceTo(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.rewindTo(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.rewindTo(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timeline.forMovie(mirrorMovie(), TestPrograms.keyMirror, keyframeInterval: 0));
    }

    [Fact]
    public void rewindingPastTheRetainedWindowFails()
    {
        Timeline timeline = Timeline.forMovie(mirrorMovie(), TestPrograms.keyMirror, keyframeInterval: 1, capacity: 3);
        timeline.advanceTo(10);

        Assert.Equal(7, timeline.rewind.oldestFrame);
        Assert.Equal(9, timeline.rewind.newestFrame);
        timeline.rewindTo(8);
        var error = Assert.Throws<InvalidOperationException>(() => timeline.rewindTo(2));
        Assert.Contains("older than the retained rewind window", error.Message);
    }

    [Fact]
    public void rewindBufferEvictsTheOldestSnapshot()
    {
        var buffer = new RewindBuffer(2);
        Assert.True(buffer.isEmpty);
        Assert.Equal(-1, buffer.oldestFrame);
        Assert.Null(buffer.nearestAtOrBefore(5));

        buffer.store(0, new byte[10]);
        buffer.store(8, new byte[20]);
        Assert.Equal(30, buffer.memoryUsed);
        Assert.Equal(8, buffer.nearestAtOrBefore(9)!.frameIndex);
        Assert.Equal(0, buffer.nearestAtOrBefore(7)!.frameIndex);

        buffer.store(16, new byte[5]);
        Assert.Equal(2, buffer.keyframeCount);
        Assert.Equal(25, buffer.memoryUsed);
        Assert.Equal(8, buffer.oldestFrame);
        Assert.Equal(16, buffer.newestFrame);
        Assert.Null(buffer.nearestAtOrBefore(7));

        buffer.clear();
        Assert.True(buffer.isEmpty);
        Assert.Equal(0, buffer.memoryUsed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RewindBuffer(0));
    }
}
