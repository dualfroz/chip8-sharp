using System;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class ReplayEngineTests
{
    static (InputMovie movie, ReplayOutcome outcome) recordSession(byte[] rom, int seed = 3)
    {
        InputMovie movie = TestPrograms.movieFor(rom, Platform.Chip8, seed);
        ReplayEngine recorder = ReplayEngine.record(MachineBuilder.fromMovie(movie, rom), movie);

        recorder.advanceFrames(5);
        recorder.pressKey(5);
        recorder.advanceFrames(8);
        recorder.releaseKey(5);
        recorder.setKeys(KeyMask.fromKeys(2, 9));
        recorder.advanceFrames(3);
        recorder.setKeys(KeyMask.empty);
        recorder.advanceFrames(4);

        return (movie, recorder.outcome());
    }

    [Fact]
    public void playbackReproducesTheRecordedSession()
    {
        (InputMovie movie, ReplayOutcome recorded) = recordSession(TestPrograms.randomWalker);

        Assert.Equal(20, movie.length);
        Assert.Equal(11, movie.entryCount);
        Assert.Equal(KeyMask.fromKeys(2, 9), movie.keysAt(14));

        ReplayOutcome replayed = ReplayEngine.replayToEnd(movie, TestPrograms.randomWalker);
        Assert.True(recorded.sameAs(replayed), recorded + " vs " + replayed);
        Assert.Equal(20, replayed.frames);
    }

    [Fact]
    public void playbackSurvivesASerializationRoundTrip()
    {
        (InputMovie movie, ReplayOutcome recorded) = recordSession(TestPrograms.randomWalker);

        InputMovie fromText = InputMovie.parseText(movie.toText());
        InputMovie fromBinary = InputMovie.parseBytes(movie.toBytes());

        Assert.True(recorded.sameAs(ReplayEngine.replayToEnd(fromText, TestPrograms.randomWalker)));
        Assert.True(recorded.sameAs(ReplayEngine.replayToEnd(fromBinary, TestPrograms.randomWalker)));
    }

    [Fact]
    public void differentInputOrSeedChangesTheOutcome()
    {
        (InputMovie movie, ReplayOutcome recorded) = recordSession(TestPrograms.randomWalker);

        InputMovie otherKeys = movie.clone();
        otherKeys.setFrame(6, KeyMask.empty);
        InputMovie otherSeed = movie.clone();
        otherSeed.randomSeed = 4;

        Assert.False(recorded.sameAs(ReplayEngine.replayToEnd(otherKeys, TestPrograms.randomWalker)));
        Assert.False(recorded.sameAs(ReplayEngine.replayToEnd(otherSeed, TestPrograms.randomWalker)));
    }

    [Fact]
    public void seededMoviesAreDeterministic()
    {
        (InputMovie movie, _) = recordSession(TestPrograms.randomWalker);
        Assert.True(ReplayEngine.isDeterministic(movie, TestPrograms.randomWalker));
    }

    [Fact]
    public void playbackRejectsLiveInput()
    {
        (InputMovie movie, _) = recordSession(TestPrograms.keyMirror);
        ReplayEngine player = ReplayEngine.playback(movie, TestPrograms.keyMirror);

        Assert.Equal(ReplayMode.Playback, player.mode);
        Assert.Throws<InvalidOperationException>(() => player.pressKey(1));
        Assert.Throws<InvalidOperationException>(() => player.setKeys(KeyMask.fromKeys(1)));
    }

    [Fact]
    public void playbackInjectsTheRecordedKeys()
    {
        (InputMovie movie, _) = recordSession(TestPrograms.keyMirror);
        ReplayEngine player = ReplayEngine.playback(movie, TestPrograms.keyMirror);

        player.advanceFrames(6);
        Assert.Equal(1, player.machine.v[1]);
        player.advanceFrames(8);
        Assert.Equal(0, player.machine.v[1]);
        player.playToEnd();
        Assert.True(player.finished);
        Assert.Equal(20, player.frameIndex);
    }

    [Fact]
    public void playbackStopsWhenTheMachineExits()
    {
        var movie = TestPrograms.movieFor(TestPrograms.exitsAfterDrawing, Platform.SuperChip);
        movie.extendLength(100);
        ReplayEngine player = ReplayEngine.playback(movie, TestPrograms.exitsAfterDrawing);

        player.playToEnd();

        Assert.Equal(1, player.frameIndex);
        Assert.True(player.outcome().exited);
        Assert.Equal(16, player.outcome().litPixels);
    }
}
