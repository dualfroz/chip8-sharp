using System.Linq;
using Chip8.Core;
using Xunit;

namespace Chip8.Analysis.Tests;

public class AnalyzerTests
{
    [Fact]
    public void profileCountsInstructionsBlocksAndKinds()
    {
        RomProfile profile = new RomAnalyzer().analyze(Programs.branchy);

        Assert.Equal(18, profile.size);
        Assert.Equal(9, profile.instructionCount);
        Assert.Equal(7, profile.blockCount);
        Assert.Equal(1, profile.subroutineCount);
        Assert.Equal(2, profile.callCount);
        Assert.Equal(3, profile.jumpCount);
        Assert.Equal(1, profile.skipCount);
        Assert.Equal(0, profile.drawCount);
        Assert.Equal(Platform.Chip8, profile.guessedPlatform);
        Assert.Equal(2, profile.kindCount(InstructionKind.Call));
        Assert.Equal(0, profile.kindCount(InstructionKind.Draw));
        Assert.Contains("  platform:      Chip8\n", profile.render());
    }

    [Theory]
    [InlineData("HIGH\nCLS", Platform.SuperChip)]
    [InlineData("DRW V0, V1, 0", Platform.SuperChip)]
    [InlineData("LD R, V3", Platform.SuperChip)]
    [InlineData("HIGH\nLD I, 0x1234", Platform.XoChip)]
    [InlineData("PLANE 2", Platform.XoChip)]
    [InlineData("SCU 4", Platform.XoChip)]
    [InlineData("CLS\nDRW V0, V1, 5", Platform.Chip8)]
    public void platformIsGuessedFromExtendedOpcodes(string source, Platform expected)
    {
        RomProfile profile = new RomAnalyzer().analyze(Programs.assemble(source + "\nhalt: JP halt"));

        Assert.Equal(expected, profile.guessedPlatform);
        Assert.Equal(expected != Platform.Chip8, profile.usesSuperChip || profile.usesXoChip);
    }

    [Fact]
    public void extendedFeaturesAreListed()
    {
        RomProfile profile = new RomAnalyzer().analyze(Programs.assemble("HIGH\nDRW V0, V1, 0\nPLANE 1\nhalt: JP halt"));

        Assert.Equal(new[] { InstructionKind.HiRes, InstructionKind.Draw }.OrderBy(kind => kind), profile.superChipFeatures);
        Assert.Equal(new[] { InstructionKind.Plane }, profile.xoChipFeatures);
    }

    [Fact]
    public void ibmLogoProfileMatchesItsListing()
    {
        byte[] rom = Programs.testRom("2-ibm-logo.ch8");
        RomProfile profile = new RomAnalyzer().analyze(rom);

        Assert.Equal(132, profile.size);
        Assert.Equal(21, profile.instructionCount);
        Assert.Equal(2, profile.blockCount);
        Assert.Equal(6, profile.drawCount);
        Assert.Equal(6, profile.kindCount(InstructionKind.LoadIndex));
        Assert.Equal(1, profile.jumpCount);
        Assert.Equal(Platform.Chip8, profile.guessedPlatform);

        var sprites = new SpriteScanner().scanDistinct(rom);
        Assert.Equal(new ushort[] { 0x22A, 0x239, 0x248, 0x257, 0x266, 0x275 }, sprites.Select(sprite => sprite.address).ToArray());
        Assert.All(sprites, sprite => Assert.Equal(15, sprite.rows));
    }

    [Fact]
    public void spriteScannerPairsIndexLoadsWithDraws()
    {
        var scanner = new SpriteScanner();
        SpriteCandidate sprite = scanner.scan(Programs.spriteWithDeadCode).Single();

        Assert.Equal(0x20C, sprite.address);
        Assert.Equal(3, sprite.rows);
        Assert.Equal(8, sprite.widthPixels);
        Assert.Equal(0x200, sprite.loadAddress);
        Assert.Equal(0x206, sprite.drawAddress);
        Assert.Equal(new byte[] { 0x81, 0x42, 0x24 }, sprite.bytes);
        Assert.Equal(6, sprite.setPixelCount);
        Assert.Equal("#......#\n.#....#.\n..#..#..\n", sprite.renderAscii());
        Assert.False(sprite.isBlank);

        SpriteCandidate region = scanner.scanRegion(Programs.spriteWithDeadCode, 0x20E, 2);
        Assert.Equal(new byte[] { 0x24, 0x00 }, region.bytes);
    }

    [Fact]
    public void fontLookupsAreNotMistakenForSprites()
    {
        byte[] program = Programs.assemble("LD I, 0x300\nLD F, V0\nDRW V0, V1, 5\nhalt: JP halt");
        Assert.Empty(new SpriteScanner().scan(program));
    }

    [Fact]
    public void coverageProfilerReportsExecutedAndMissedCode()
    {
        CoverageReport report = new CoverageProfiler().profile(Programs.branchy, 1000);

        Assert.True(report.haltedOnSpin);
        Assert.Equal(9, report.totalSteps);
        Assert.Equal(8, report.executedInstructionCount);
        Assert.Equal(9, report.reachableInstructionCount);
        Assert.Equal(800.0 / 9.0, report.coveragePercent, 9);
        Assert.Equal(new ushort[] { 0x206 }, report.reachableButNotExecuted.ToArray());
        Assert.Empty(report.executedOutsideReachable);
        Assert.Equal(((ushort)0x210, 2), report.hotspot());
        Assert.Equal(1, report.executionCount(0x20C));
        Assert.Equal(0, report.executionCount(0x206));
        Assert.Equal(1, report.opcodeCounts[0x220C]);
        Assert.Equal(3, report.opcodeCounts[0x1210]);
    }

    [Fact]
    public void coverageProfilerRespectsTheStepLimit()
    {
        CoverageReport report = new CoverageProfiler().profile(Programs.branchy, 3, stopOnSpin: false);

        Assert.False(report.haltedOnSpin);
        Assert.Equal(3, report.totalSteps);
    }

    [Fact]
    public void traceRecorderCapturesStateBeforeEachInstruction()
    {
        TraceRecorder trace = TraceRecorder.record(Programs.branchy, 1000);

        Assert.True(trace.haltedOnSpin);
        Assert.Equal(9, trace.count);
        Assert.Equal(
            new ushort[] { 0x200, 0x202, 0x204, 0x208, 0x20C, 0x20E, 0x20A, 0x210 },
            trace.visitedAddresses().ToArray());
        Assert.Equal("LD V0, 0x00", trace[0].mnemonic);
        Assert.Equal(1, trace[4].stackPointer);
        Assert.Equal(0, trace[4].register(0));
        Assert.Equal(1, trace[5].register(0));
        Assert.StartsWith("V0=01 V1=00", trace[5].registerDump());
        Assert.Contains("0x020C  700", trace.render());
    }

    [Fact]
    public void selfModifyingCodeIsDetectedDynamicallyAndStatically()
    {
        SelfModifyingCodeReport report = new SelfModifyingCodeDetector().detect(Programs.selfModifying, 100);

        Assert.True(report.detected);
        SmcEvent write = report.events.Single();
        Assert.Equal(3, write.step);
        Assert.Equal(0x206, write.writerAddress);
        Assert.Equal(0x209, write.targetAddress);
        Assert.Equal(0x00, write.oldValue);
        Assert.Equal(0x2A, write.newValue);
        Assert.Equal(new ushort[] { 0x209 }, report.modifiedAddresses.ToArray());
        Assert.True(report.hasStaticSuspicion);
        Assert.Equal(new ushort[] { 0x208 }, report.staticTargets.ToArray());
        Assert.Equal(0x206, report.staticWriters.Single().address);

        TraceRecorder trace = TraceRecorder.record(Programs.selfModifying, 100);
        Assert.Equal((ushort)0x602A, trace.entries.First(entry => entry.address == 0x208).opcode);
    }

    [Fact]
    public void cleanProgramHasNoSelfModifyingCode()
    {
        SelfModifyingCodeReport report = new SelfModifyingCodeDetector().detect(Programs.spriteWithDeadCode, 100);

        Assert.False(report.detected);
        Assert.False(report.hasStaticSuspicion);
        Assert.Equal("self-modifying code: none\n", report.render());
    }
}
