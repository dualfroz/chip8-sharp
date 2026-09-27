using System;
using System.IO;
using Chip8.Core;
using Chip8.OctoAsm;
using Xunit;

namespace Chip8.OctoAsm.Tests;

public class OctoProgramTests
{
    static Machine run(string source, int steps = 4000, Action<Machine>? prepare = null)
    {
        var machine = new Machine(Quirks.modern(), 1);
        machine.loadRom(OctoAssembler.assembleRom(source));
        prepare?.Invoke(machine);
        for (int step = 0; step < steps; step++)
        {
            machine.step();
        }
        return machine;
    }

    [Fact]
    public void mainFirstNeedsNoEntryJump()
    {
        OctoProgram program = OctoAssembler.assemble(": main clear");

        Assert.Equal(new byte[] { 0x00, 0xE0 }, program.rom);
        Assert.Equal(0x200, program.labelAddress("main"));
    }

    [Fact]
    public void dataBeforeMainGetsAnEntryJump()
    {
        OctoProgram program = OctoAssembler.assemble(": data 1 2\n: main clear");

        Assert.Equal(new byte[] { 0x12, 0x04, 0x01, 0x02, 0x00, 0xE0 }, program.rom);
        Assert.Equal(0x202, program.labelAddress("data"));
        Assert.Equal(0x204, program.labelAddress("main"));
    }

    [Fact]
    public void declarationsBeforeMainDoNotForceAnEntryJump()
    {
        byte[] rom = OctoAssembler.assembleRom(":const X 5\n:alias r v3\n:macro wipe { clear }\n: main wipe r := X");

        Assert.Equal(new byte[] { 0x00, 0xE0, 0x63, 0x05 }, rom);
    }

    [Fact]
    public void forwardReferencesArePatched()
    {
        byte[] rom = OctoAssembler.assembleRom(": main sub :call sub jump done\n: sub ;\n: done loop again");

        Assert.Equal(Convert.FromHexString("22062206120800EE1208"), rom);
    }

    [Fact]
    public void orgPointerNextAndBreakpointTrackAddresses()
    {
        OctoProgram program = OctoAssembler.assemble(
            ": main :next patch v0 := 7 :pointer far :breakpoint stop jump far\n:org 0x300 : far clear");

        Assert.Equal(0x201, program.labelAddress("patch"));
        Assert.Equal(0x300, program.labelAddress("far"));
        Assert.Equal(0x204, program.breakpoints["stop"]);
        Assert.Equal(0x102, program.rom.Length);
        Assert.Equal(new byte[] { 0x60, 0x07, 0x03, 0x00, 0x13, 0x00 }, program.rom[..6]);
        Assert.Equal(new byte[] { 0x00, 0xE0 }, program.rom[0x100..]);
    }

    [Fact]
    public void unpackLoadsAddressIntoV0AndV1()
    {
        Assert.Equal(Convert.FromHexString("60A2610400E0"), OctoAssembler.assembleRom(": main :unpack 0xA target\n: target clear"));
        Assert.Equal(Convert.FromHexString("6002610400E0"), OctoAssembler.assembleRom(": main :unpack long target\n: target clear"));
    }

    [Fact]
    public void calcSeesLabelsAndHere()
    {
        OctoProgram program = OctoAssembler.assemble(": main clear : data 0 0 :calc after { HERE } :calc size { after - data }");

        Assert.Equal(0x204, program.constants["after"]);
        Assert.Equal(2, program.constants["size"]);
    }

    [Fact]
    public void macrosSubstituteArgumentsAndKeepNestedBraces()
    {
        byte[] rom = OctoAssembler.assembleRom(
            ":macro set REG VAL { :calc tmp { VAL + 1 } REG := tmp }\n: main set v2 8 set v3 9");

        Assert.Equal(Convert.FromHexString("6209630A"), rom);
    }

    [Fact]
    public void ifBeginElseEndJumpsAroundBranches()
    {
        byte[] rom = OctoAssembler.assembleRom(": main if v0 == 1 begin v1 := 1 else v1 := 2 end");

        Assert.Equal(Convert.FromHexString("300112086101120A6102"), rom);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    public void ifBeginElseEndRunsTheRightBranch(int input, int expected)
    {
        Machine machine = run($": main v0 := {input} if v0 == 1 begin v1 := 1 else v1 := 2 end loop again", 20);
        Assert.Equal(expected, machine.v[1]);
    }

    [Fact]
    public void loopWhileAgainCountsUpToTheCondition()
    {
        byte[] rom = OctoAssembler.assembleRom(": main loop while v0 != 5 v0 += 1 again");
        Assert.Equal(Convert.FromHexString("4005120870011200"), rom);

        Machine machine = run(": main loop while v0 != 5 v0 += 1 again v1 := 0xAA loop again", 200);
        Assert.Equal(5, machine.v[0]);
        Assert.Equal(0xAA, machine.v[1]);
    }

    [Fact]
    public void keyConditionsFollowTheKeypad()
    {
        const string source = ": main v0 := 7 if v0 key then v1 := 1 if v0 -key then v2 := 1 loop again";

        Machine pressed = run(source, 20, machine => machine.pressKey(7));
        Assert.Equal(1, pressed.v[1]);
        Assert.Equal(0, pressed.v[2]);

        Machine released = run(source, 20);
        Assert.Equal(0, released.v[1]);
        Assert.Equal(1, released.v[2]);
    }

    public static TheoryData<string, bool> relationalCases() => new()
    {
        { "<", true }, { "<", false },
        { ">", true }, { ">", false },
        { "<=", true }, { "<=", false },
        { ">=", true }, { ">=", false },
    };

    [Theory]
    [MemberData(nameof(relationalCases))]
    public void relationalComparisonsMatchUnsignedSemantics(string op, bool registerOperand)
    {
        int[][] pairs =
        {
            new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 5, 5 }, new[] { 127, 128 },
            new[] { 128, 127 }, new[] { 254, 255 }, new[] { 255, 254 }, new[] { 255, 255 }, new[] { 0, 255 },
        };

        foreach (int[] pair in pairs)
        {
            int left = pair[0];
            int right = pair[1];
            string rhs = registerOperand ? "v2" : right.ToString();
            string source =
                $": main v1 := {left} v2 := {right}\n" +
                $"if v1 {op} {rhs} then v3 := 1\n" +
                $"if v1 {op} {rhs} begin v4 := 1 else v4 := 2 end\n" +
                "loop again";

            Machine machine = run(source, 40);
            bool expected = op switch
            {
                "<" => left < right,
                ">" => left > right,
                "<=" => left <= right,
                _ => left >= right,
            };

            Assert.True(expected == (machine.v[3] == 1), $"then: {left} {op} {right}");
            Assert.True(machine.v[4] == (expected ? 1 : 2), $"begin/else: {left} {op} {right}");
            Assert.Equal(left, machine.v[1]);
        }
    }

    [Fact]
    public void listingMarksDataAndDecodesLongLoadsAsOneInstruction()
    {
        string listing = OctoAssembler.assemble(": glyph 0xF0 0x90\n: main i := long glyph clear").renderListing();

        Assert.Contains("                glyph:\n0x0202:            DB 0xF0, 0x90\n", listing);
        Assert.Contains("0x0204:  F000 0202 LD I, 0x0202\n", listing);
        Assert.Contains("0x0208:  00E0      CLS\n", listing);
    }

    [Fact]
    public void moverExampleAssemblesAndMovesTheBallWithTheKeypad()
    {
        string source = File.ReadAllText(Path.Combine(locateExamples(), "mover.8o"));
        OctoProgram program = OctoAssembler.assemble(source);

        var machine = new Machine(Quirks.modern(), 1);
        machine.loadRom(program.rom);
        machine.pressKey(9);
        for (int frame = 0; frame < 30; frame++)
        {
            machine.runFrame(11);
        }

        Assert.True(machine.v[0xA] > 30, "ball-x should have moved right");
        Assert.Equal(14, machine.v[0xB]);
        Assert.Equal(machine.v[0xA] - 30, machine.v[0xC]);
    }

    static string locateExamples()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "examples");
            if (File.Exists(Path.Combine(candidate, "mover.8o")))
            {
                return candidate;
            }
        }
        throw new DirectoryNotFoundException("examples directory not found");
    }
}
