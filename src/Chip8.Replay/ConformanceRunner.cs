using System.Collections.Generic;
using Chip8.Core;

namespace Chip8.Replay;

public sealed class ConformanceRunner
{
    readonly RomLibrary library;

    public ConformanceRunner(RomLibrary library)
    {
        this.library = library;
    }

    public static ConformanceRunner locate() => new ConformanceRunner(RomLibrary.locate());

    public RomLibrary roms => library;

    public Machine execute(RomExpectation expectation, out int cyclesExecuted)
    {
        var machine = new Machine(QuirksCodec.clone(expectation.quirks), expectation.randomSeed, expectation.platform);
        machine.loadRom(library.load(expectation.romName));

        int cycle = 0;
        for (; cycle < expectation.cycles && !machine.exited; cycle++)
        {
            machine.step();
            if (cycle % expectation.instructionsPerFrame == expectation.instructionsPerFrame - 1)
            {
                machine.tickTimers();
            }
        }

        cyclesExecuted = cycle;
        return machine;
    }

    public TestRomResult run(RomExpectation expectation)
    {
        Machine machine = execute(expectation, out int cyclesExecuted);
        FramebufferSnapshot snapshot = FramebufferSnapshot.capture(machine);
        int litPixels = snapshot.litCount;
        ulong framebufferHash = FramebufferHash.compute(snapshot);
        ulong registerHash = FramebufferHash.computeWithRegisters(machine);

        bool rangeOk = expectation.litPixelsInRange(litPixels);
        bool hashOk = !expectation.framebufferHash.HasValue || expectation.framebufferHash.Value == framebufferHash;
        bool passed = rangeOk && hashOk;

        string detail = passed
            ? expectation.description
            : buildFailureDetail(expectation, litPixels, framebufferHash, rangeOk, hashOk);

        return new TestRomResult(
            expectation.romName,
            passed,
            litPixels,
            expectation.minLitPixels,
            expectation.maxLitPixels,
            framebufferHash,
            registerHash,
            cyclesExecuted,
            machine.exited,
            detail);
    }

    public IReadOnlyList<TestRomResult> runAll(IEnumerable<RomExpectation> expectations)
    {
        var results = new List<TestRomResult>();
        foreach (RomExpectation expectation in expectations)
        {
            results.Add(run(expectation));
        }
        return results;
    }

    public IReadOnlyList<TestRomResult> runBuiltIns() => runAll(builtInExpectations());

    static string buildFailureDetail(RomExpectation expectation, int litPixels, ulong framebufferHash, bool rangeOk, bool hashOk)
    {
        if (!rangeOk)
        {
            return "lit " + litPixels + " outside [" + expectation.minLitPixels + ".." + expectation.maxLitPixels + "]";
        }
        if (!hashOk)
        {
            return "hash " + FramebufferHash.format(framebufferHash)
                + " != expected " + FramebufferHash.format(expectation.framebufferHash!.Value);
        }
        return "unexpected failure";
    }

    public static IReadOnlyList<RomExpectation> builtInExpectations() => new List<RomExpectation>
    {
        new RomExpectation("2-ibm-logo.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 400, minLitPixels: 300, maxLitPixels: 340, description: "IBM logo"),
        new RomExpectation("1-chip8-logo.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 400, minLitPixels: 400, maxLitPixels: 620, description: "CHIP-8 splash logo"),
        new RomExpectation("3-corax+.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 4000, minLitPixels: 400, maxLitPixels: 1200, description: "corax+ opcode suite"),
        new RomExpectation("4-flags.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 6000, minLitPixels: 400, maxLitPixels: 1400, description: "flags suite"),
        new RomExpectation("test_opcode.ch8", Platform.Chip8, Quirks.modern(),
            cycles: 4000, minLitPixels: 200, maxLitPixels: 1000, description: "opcode test grid"),
        new RomExpectation("BC_test.ch8", Platform.Chip8, Quirks.cosmacVip(),
            cycles: 4000, minLitPixels: 80, maxLitPixels: 900, description: "BestCoder test"),
    };
}
