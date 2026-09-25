using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Chip8.Analysis;
using Chip8.Core;

namespace Chip8.Cli;

static class AnalyzeCommand
{
    const int allSteps = 100_000;

    sealed class Request
    {
        public string romPath = "";
        public bool cfg;
        public bool calls;
        public bool sprites;
        public bool dead;
        public int coverageSteps;
        public bool heatmap;
        public int smcSteps;
        public int traceSteps;
        public string? platformName;
        public string quirksName = "auto";
        public int seed = 1;
    }

    public static int run(string[] args)
    {
        if (args.Length < 1 || CliArgs.isHelp(args))
        {
            printHelp();
            return args.Length < 1 ? 1 : 0;
        }

        Request request = parse(args);
        byte[] rom = CliArgs.readRom(request.romPath);
        RomProfile profile = new RomAnalyzer().analyze(rom);
        ReachabilitySet reachability = ReachabilitySet.build(rom);
        Platform platform = request.platformName is null ? profile.guessedPlatform : Options.platformFrom(request.platformName);
        Quirks? quirks = Options.quirksFrom(request.quirksName);

        var output = new StringBuilder();
        output.Append(profile.render());
        output.Append("  reachable:     ").Append(reachability.reachableInstructionCount).Append(" instructions, ")
            .Append(reachability.reachableByteCount).Append(" of ").Append(reachability.programByteCount).Append(" bytes (")
            .Append((reachability.reachableByteFraction * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("%)\n");

        if (request.cfg)
        {
            output.Append('\n').Append(ControlFlowGraph.build(rom).render());
        }

        if (request.calls)
        {
            output.Append('\n').Append(CallGraph.build(rom).render());
        }

        if (request.sprites)
        {
            var scanner = new SpriteScanner();
            IReadOnlyList<SpriteCandidate> candidates = scanner.scanDistinct(rom);
            output.Append('\n').Append(candidates.Count).Append(" sprite(s) referenced by draw instructions\n");
            output.Append(scanner.render(candidates));
        }

        if (request.dead)
        {
            output.Append('\n').Append(renderDead(reachability, rom));
        }

        if (request.coverageSteps > 0)
        {
            CoverageReport coverage = new CoverageProfiler()
                .profile(rom, request.coverageSteps, platform, quirks, request.seed);
            output.Append('\n').Append(renderCoverage(coverage, request.heatmap));
        }

        if (request.smcSteps > 0)
        {
            SelfModifyingCodeReport smc = new SelfModifyingCodeDetector()
                .detect(rom, request.smcSteps, platform, quirks, request.seed);
            output.Append('\n').Append(smc.render());
            if (smc.hasStaticSuspicion)
            {
                output.Append("  static: ").Append(smc.staticWriters.Count).Append(" memory writer(s), I loaded with code address(es)");
                foreach (ushort target in smc.staticTargets)
                {
                    output.Append(' ').Append(hex(target));
                }
                output.Append('\n');
            }
        }

        if (request.traceSteps > 0)
        {
            TraceRecorder trace = TraceRecorder.record(rom, request.traceSteps, platform, quirks, request.seed);
            output.Append('\n').Append("trace: ").Append(trace.count).Append(" instruction(s)")
                .Append(trace.haltedOnSpin ? ", stopped at a spin loop" : "").Append('\n');
            output.Append(trace.render());
        }

        Console.Write(output.ToString());
        return 0;
    }

    static Request parse(string[] args)
    {
        var request = new Request { romPath = args[0] };
        for (int index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--cfg":
                    request.cfg = true;
                    break;
                case "--calls":
                    request.calls = true;
                    break;
                case "--sprites":
                    request.sprites = true;
                    break;
                case "--dead":
                    request.dead = true;
                    break;
                case "--coverage":
                    request.coverageSteps = CliArgs.positive(args, ref index);
                    break;
                case "--heatmap":
                    request.heatmap = true;
                    break;
                case "--smc":
                    request.smcSteps = CliArgs.positive(args, ref index);
                    break;
                case "--trace":
                    request.traceSteps = CliArgs.positive(args, ref index);
                    break;
                case "--all":
                    request.cfg = request.calls = request.sprites = request.dead = true;
                    request.coverageSteps = request.coverageSteps > 0 ? request.coverageSteps : allSteps;
                    request.smcSteps = request.smcSteps > 0 ? request.smcSteps : allSteps;
                    break;
                case "--platform":
                    request.platformName = CliArgs.value(args, ref index);
                    break;
                case "--quirks":
                    request.quirksName = CliArgs.value(args, ref index);
                    break;
                case "--seed":
                    request.seed = CliArgs.integer(args, ref index);
                    break;
                default:
                    throw new ArgumentException("unknown analyze option " + args[index]);
            }
        }
        return request;
    }

    static string renderDead(ReachabilitySet reachability, byte[] rom)
    {
        var builder = new StringBuilder();
        IReadOnlyList<InstructionInfo> dead = reachability.deadInstructions(rom);
        builder.Append("unreachable words (linear decode): ").Append(dead.Count).Append('\n');

        IReadOnlyList<ushort> dataBytes = reachability.dataByteAddresses();
        builder.Append("bytes never reached as code: ").Append(dataBytes.Count).Append('\n');
        int start = 0;
        while (start < dataBytes.Count)
        {
            int end = start;
            while (end + 1 < dataBytes.Count && dataBytes[end + 1] == dataBytes[end] + 1)
            {
                end++;
            }
            builder.Append("  ").Append(hex(dataBytes[start]));
            if (end > start)
            {
                builder.Append("..").Append(hex(dataBytes[end]));
            }
            builder.Append(" (").Append(end - start + 1).Append(" bytes)\n");
            start = end + 1;
        }
        return builder.ToString();
    }

    static string renderCoverage(CoverageReport coverage, bool heatmap)
    {
        if (heatmap)
        {
            return coverage.renderHeatmap();
        }

        var builder = new StringBuilder();
        (ushort address, int count) = coverage.hotspot();
        builder.Append("coverage: ").Append(coverage.coveragePercent.ToString("F1", CultureInfo.InvariantCulture))
            .Append("% of reachable instructions over ").Append(coverage.totalSteps).Append(" steps")
            .Append(coverage.haltedOnSpin ? " (stopped at a spin loop)" : "").Append('\n');
        builder.Append("  executed:      ").Append(coverage.executedInstructionCount).Append(" distinct addresses\n");
        builder.Append("  not executed:  ").Append(coverage.reachableButNotExecuted.Count).Append(" reachable addresses\n");
        builder.Append("  outside cfg:   ").Append(coverage.executedOutsideReachable.Count).Append(" executed addresses\n");
        builder.Append("  hotspot:       ").Append(hex(address)).Append(" x").Append(count).Append('\n');
        return builder.ToString();
    }

    static string hex(ushort address) => "0x" + address.ToString("X4", CultureInfo.InvariantCulture);

    static void printHelp()
    {
        Console.WriteLine("usage: chip8 analyze <rom> [options]");
        Console.WriteLine();
        Console.WriteLine("Prints a ROM profile (size, instruction and block counts, subroutines, draw/call/jump/skip");
        Console.WriteLine("counts, guessed platform, instruction histogram) and the reachable share of the ROM.");
        Console.WriteLine();
        Console.WriteLine("static passes:");
        Console.WriteLine("  --cfg             control flow graph: basic blocks and their edges");
        Console.WriteLine("  --calls           call graph: subroutines, callers, recursion, orphaned targets");
        Console.WriteLine("  --sprites         sprites referenced by I-load + draw pairs, as ASCII art");
        Console.WriteLine("  --dead            unreachable words and byte ranges never reached as code");
        Console.WriteLine();
        Console.WriteLine("dynamic passes (run the ROM, stop early on exit or a jump-to-self loop):");
        Console.WriteLine("  --coverage N      execute N instructions, report coverage and the hotspot");
        Console.WriteLine("  --heatmap         with --coverage: print the execution count of every address");
        Console.WriteLine("  --smc N           execute N instructions, report writes into code bytes");
        Console.WriteLine("  --trace N         print the first N executed instructions with I and SP");
        Console.WriteLine("  --all             --cfg --calls --sprites --dead, coverage and smc over 100000 steps");
        Console.WriteLine();
        Console.WriteLine("  --platform NAME   chip8, superchip or xochip (default: guessed from the opcodes)");
        Console.WriteLine("  --quirks NAME     auto (default), modern, cosmac, superchip, xochip");
        Console.WriteLine("  --seed N          RNG seed for dynamic passes (default 1)");
    }
}
