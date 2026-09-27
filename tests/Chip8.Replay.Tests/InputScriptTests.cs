using System.Linq;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class InputScriptTests
{
    [Fact]
    public void parsesEveryCommandKind()
    {
        InputScript script = InputScript.parse(string.Join('\n', new[]
        {
            "# setup",
            "press 5",
            "release 0x5",
            "hold A for 3   // three frames",
            "wait 2",
            "step 1",
            "frame 20: press F",
            "assert v3 == 0x10",
            "assert i != 512",
            "assert pc == 0x200",
            "assert pixel 4 5 off",
            "assert exited",
            "assert running",
        }));

        Assert.Equal(12, script.count);
        Assert.Equal(
            new[]
            {
                "press 5", "release 5", "hold A for 3", "wait 2", "wait 1", "frame 20: press F",
                "assert v3 == 16", "assert i != 512", "assert pc == 512", "assert pixel 4 5 off",
                "assert exited", "assert running",
            },
            script.commands.Select(command => command.describe()).ToArray());
        Assert.Equal(2, script.commands[0].sourceLine);
        Assert.IsType<FrameCommand>(script.commands[5]);
    }

    [Theory]
    [InlineData("jump 3", 1, "unknown command 'jump'")]
    [InlineData("press 5\nhold 5 3", 2, "expected 'hold <key> for <frames>'")]
    [InlineData("press 16", 1, "out of range 0..15")]
    [InlineData("frame 3 press 1", 1, "needs a ':'")]
    [InlineData("frame -1: press 1", 1, "cannot be negative")]
    [InlineData("frame 3: frame 4: press 1", 1, "cannot be nested")]
    [InlineData("frame 3:", 1, "must be followed by a command")]
    [InlineData("assert v0 == 300", 1, "out of range 0..255")]
    [InlineData("assert v0 < 3", 1, "unknown comparison '<'")]
    [InlineData("assert pixel 1 2 maybe", 1, "must be 'on' or 'off'")]
    [InlineData("assert sound", 1, "unknown assert target")]
    [InlineData("wait x", 1, "invalid number")]
    public void reportsParseErrorsWithLineNumbers(string text, int line, string message)
    {
        var error = Assert.Throws<ScriptParseException>(() => InputScript.parse(text));
        Assert.Equal(line, error.line);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void runnerHoldsKeysAcrossFramesAndChecksAssertions()
    {
        var runner = ScriptRunner.forRom(TestPrograms.keyMirror, Platform.Chip8, null, TestPrograms.instructionsPerFrame);
        runner.run(InputScript.parse("hold 5 for 3\nassert v1 == 1\nwait 2\nassert v1 == 0\nassert pixel 0 0 on\nassert pixel 4 0 off\nassert running"));

        Assert.Equal(5, runner.currentFrame);
        Assert.Equal(5, runner.assertionsPassed);
        Assert.Equal(KeyMask.empty, runner.heldKeys);
    }

    [Fact]
    public void runnerMatchesDirectlyDrivenMachine()
    {
        var runner = ScriptRunner.forRom(TestPrograms.randomWalker, Platform.Chip8, null, TestPrograms.instructionsPerFrame, 7);
        runner.run(InputScript.parse("wait 4\nframe 10: press 5\nframe 16: release 5\nwait 3"));

        var direct = TestPrograms.machineFor(TestPrograms.randomWalker, Platform.Chip8, 7);
        for (int frame = 0; frame < 19; frame++)
        {
            direct.keypad[5] = frame >= 10 && frame < 16;
            direct.runFrame(TestPrograms.instructionsPerFrame);
        }

        Assert.Equal(19, runner.currentFrame);
        Assert.Equal(direct.saveState(), runner.machine.saveState());
    }

    [Fact]
    public void failedAssertionReportsLineFrameAndValues()
    {
        var runner = ScriptRunner.forRom(TestPrograms.keyMirror, Platform.Chip8, null, TestPrograms.instructionsPerFrame);
        var error = Assert.Throws<ScriptAssertionException>(() =>
            runner.run(InputScript.parse("wait 2\n\nassert v1 == 1")));

        Assert.Equal(3, error.line);
        Assert.Equal(2, error.frame);
        Assert.Contains("expected V1 == 1 but was 0", error.Message);
    }

    [Fact]
    public void pixelOutsideTheFramebufferFailsTheAssertion()
    {
        var runner = ScriptRunner.forRom(TestPrograms.keyMirror, Platform.Chip8, null, TestPrograms.instructionsPerFrame);
        var error = Assert.Throws<ScriptAssertionException>(() => runner.run(InputScript.parse("assert pixel 64 0 on")));
        Assert.Contains("outside the 64x32 framebuffer", error.Message);
    }

    [Fact]
    public void frameCommandsCannotGoBackInTime()
    {
        var runner = ScriptRunner.forRom(TestPrograms.keyMirror, Platform.Chip8, null, TestPrograms.instructionsPerFrame);
        var error = Assert.Throws<ScriptException>(() => runner.run(InputScript.parse("wait 10\nframe 5: press 1")));

        Assert.Equal(2, error.line);
        Assert.Contains("already at frame 10", error.Message);
    }

    [Fact]
    public void exitAssertionsFollowTheMachine()
    {
        var runner = ScriptRunner.forRom(TestPrograms.exitsAfterDrawing, Platform.SuperChip, null, TestPrograms.instructionsPerFrame);
        runner.run(InputScript.parse("wait 1\nassert exited\nassert pixel 100 40 on"));
        Assert.Equal(2, runner.assertionsPassed);

        var stillRunning = ScriptRunner.forRom(TestPrograms.keyMirror, Platform.Chip8, null, TestPrograms.instructionsPerFrame);
        Assert.Throws<ScriptAssertionException>(() => stillRunning.run(InputScript.parse("wait 1\nassert exited")));
    }

    [Fact]
    public void runnerRecordsHeldKeysIntoAMovie()
    {
        InputMovie movie = TestPrograms.movieFor(TestPrograms.keyMirror);
        var machine = MachineBuilder.fromMovie(movie, TestPrograms.keyMirror);
        var runner = new ScriptRunner(machine, movie.instructionsPerFrame, movie);
        runner.run(InputScript.parse("wait 1\nhold 5 for 2\npress 9\nwait 1\nwait 2"));

        Assert.Equal(6, movie.length);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, movie.recordedIndices.ToArray());
        Assert.Equal(KeyMask.fromKeys(5), movie.keysAt(1));
        Assert.Equal(KeyMask.fromKeys(9), movie.keysAt(3));
        Assert.Equal(KeyMask.empty, movie.keysAt(0));
    }
}
