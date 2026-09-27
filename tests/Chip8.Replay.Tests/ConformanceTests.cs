using System.IO;
using System.Linq;
using Chip8.Core;
using Xunit;

namespace Chip8.Replay.Tests;

public class ConformanceTests
{
    [Fact]
    public void libraryLocatesTheBundledTestRoms()
    {
        RomLibrary library = RomLibrary.locate();

        Assert.EndsWith(Path.Combine("tests", "roms"), library.directory);
        Assert.Contains("2-ibm-logo.ch8", library.available());
        Assert.Equal(library.available().OrderBy(name => name, System.StringComparer.Ordinal), library.available());
        Assert.Same(library.load("2-ibm-logo.ch8"), library.load("2-ibm-logo.ch8"));
        Assert.Throws<FileNotFoundException>(() => library.load("missing.ch8"));
    }

    [Fact]
    public void everyBuiltInExpectationPasses()
    {
        var runner = ConformanceRunner.locate();

        foreach (TestRomResult outcome in runner.runBuiltIns())
        {
            Assert.True(outcome.passed, outcome.format());
            Assert.StartsWith("PASS " + outcome.romName, outcome.format());
        }
    }

    [Fact]
    public void ibmLogoMatchesItsKnownPixelCountAndHashIsStable()
    {
        var runner = ConformanceRunner.locate();
        var expectation = new RomExpectation("2-ibm-logo.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 400, minLitPixels: 230, maxLitPixels: 230);

        TestRomResult first = runner.run(expectation);
        TestRomResult pinned = runner.run(expectation.withHash(first.framebufferHash));

        Assert.True(first.passed, first.format());
        Assert.True(pinned.passed, pinned.format());
        Assert.Equal(230, first.litPixels);
        Assert.Equal(400, first.cyclesExecuted);
    }

    [Fact]
    public void mismatchesAreReportedWithDetail()
    {
        var runner = ConformanceRunner.locate();
        var outOfRange = new RomExpectation("2-ibm-logo.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 400, minLitPixels: 300, maxLitPixels: 340);
        var wrongHash = new RomExpectation("2-ibm-logo.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 400, minLitPixels: 0, maxLitPixels: 5000, framebufferHash: 1);

        TestRomResult rangeResult = runner.run(outOfRange);
        TestRomResult hashResult = runner.run(wrongHash);

        Assert.False(rangeResult.passed);
        Assert.Equal("lit 230 outside [300..340]", rangeResult.detail);
        Assert.False(hashResult.passed);
        Assert.Contains("!= expected 0000000000000001", hashResult.detail);
        Assert.StartsWith("FAIL 2-ibm-logo.ch8 lit=230", hashResult.format());
    }
}
