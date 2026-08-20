using System.Collections.Generic;
using Chip8.Core;
using Chip8.Core.Assembly;
using Xunit;

namespace Chip8.Tests;

public class AssemblerTests
{
    static byte[] assemble(string source) => Assembler.assembleRom(source);

    [Theory]
    [InlineData("LD V1, 0x2A", 0x61, 0x2A)]
    [InlineData("ADD VA, 0xFF", 0x7A, 0xFF)]
    [InlineData("LD I, 0x300", 0xA3, 0x00)]
    [InlineData("DRW V0, V1, 5", 0xD0, 0x15)]
    [InlineData("JP 0x2A0", 0x12, 0xA0)]
    [InlineData("CALL 0x2B0", 0x22, 0xB0)]
    [InlineData("SE V3, 0x10", 0x33, 0x10)]
    [InlineData("SNE V3, V4", 0x93, 0x40)]
    [InlineData("SE V3, V4", 0x53, 0x40)]
    [InlineData("OR V1, V2", 0x81, 0x21)]
    [InlineData("AND V1, V2", 0x81, 0x22)]
    [InlineData("XOR V1, V2", 0x81, 0x23)]
    [InlineData("SUB V1, V2", 0x81, 0x25)]
    [InlineData("SUBN V1, V2", 0x81, 0x27)]
    [InlineData("SHR V5", 0x85, 0x06)]
    [InlineData("SHL V5", 0x85, 0x0E)]
    [InlineData("RND V0, 0x0F", 0xC0, 0x0F)]
    [InlineData("ADD I, V2", 0xF2, 0x1E)]
    [InlineData("LD [I], V5", 0xF5, 0x55)]
    [InlineData("LD V5, [I]", 0xF5, 0x65)]
    [InlineData("LD F, V0", 0xF0, 0x29)]
    [InlineData("LD B, V3", 0xF3, 0x33)]
    [InlineData("LD DT, V1", 0xF1, 0x15)]
    [InlineData("LD V1, DT", 0xF1, 0x07)]
    [InlineData("LD V1, K", 0xF1, 0x0A)]
    [InlineData("JP V0, 0x2C0", 0xB2, 0xC0)]
    public void encodesCoreInstructions(string source, int high, int low)
    {
        Assert.Equal(new byte[] { (byte)high, (byte)low }, assemble(source));
    }

    [Theory]
    [InlineData("SCD 4", 0x00, 0xC4)]
    [InlineData("SCU 6", 0x00, 0xD6)]
    [InlineData("SCR", 0x00, 0xFB)]
    [InlineData("SCL", 0x00, 0xFC)]
    [InlineData("EXIT", 0x00, 0xFD)]
    [InlineData("LOW", 0x00, 0xFE)]
    [InlineData("HIGH", 0x00, 0xFF)]
    [InlineData("PLANE 2", 0xF2, 0x01)]
    [InlineData("AUDIO", 0xF0, 0x02)]
    [InlineData("SAVE V0-V5", 0x50, 0x52)]
    [InlineData("LOAD V2-V4", 0x52, 0x43)]
    [InlineData("LD HF, V0", 0xF0, 0x30)]
    [InlineData("PITCH V3", 0xF3, 0x3A)]
    [InlineData("LD R, V2", 0xF2, 0x75)]
    [InlineData("LD V2, R", 0xF2, 0x85)]
    public void encodesExtendedInstructions(string source, int high, int low)
    {
        Assert.Equal(new byte[] { (byte)high, (byte)low }, assemble(source));
    }

    [Fact]
    public void longLoadEmitsFourBytesForLargeIndex()
    {
        Assert.Equal(new byte[] { 0xF0, 0x00, 0x12, 0x34 }, assemble("LD I, 0x1234"));
    }

    [Fact]
    public void equConstantIsSubstituted()
    {
        Assert.Equal(new byte[] { 0x60, 0x08 }, assemble("WIDTH EQU 8\nLD V0, WIDTH"));
    }

    [Theory]
    [InlineData("LD V0, (2 + 3) * 4", 0x60, 0x14)]
    [InlineData("LD V1, 0xFF & 0x0F", 0x61, 0x0F)]
    [InlineData("LD V2, 1 << 4", 0x62, 0x10)]
    [InlineData("LD V3, 0x30 >> 2", 0x63, 0x0C)]
    [InlineData("LD V4, ~0xF0 & 0xFF", 0x64, 0x0F)]
    [InlineData("LD V5, 10 - 3", 0x65, 0x07)]
    public void evaluatesExpressions(string source, int high, int low)
    {
        Assert.Equal(new byte[] { (byte)high, (byte)low }, assemble(source));
    }

    [Fact]
    public void radixLiteralsDecodeToTheSameBytes()
    {
        Assert.Equal(new byte[] { 0xAA, 0xF0, 0x90, 0x0F }, assemble("DB %10101010, $F0, 144, 0b00001111"));
    }

    [Fact]
    public void dataWordsAreBigEndian()
    {
        Assert.Equal(new byte[] { 0x12, 0x34, 0xAB, 0xCD }, assemble("DW 0x1234, 0xABCD"));
    }

    [Fact]
    public void orgLeavesAGapBeforeCode()
    {
        byte[] rom = assemble("ORG 0x210\nLD V0, 1");
        Assert.Equal(18, rom.Length);
        Assert.Equal(0x60, rom[16]);
        Assert.Equal(0x01, rom[17]);
        Assert.Equal(0x00, rom[0]);
    }

    [Fact]
    public void alignAdvancesToBoundary()
    {
        byte[] rom = assemble("LD V0, 1\nALIGN 4\nLD V1, 2");
        Assert.Equal(new byte[] { 0x60, 0x01, 0x00, 0x00, 0x61, 0x02 }, rom);
    }

    [Fact]
    public void labelResolvesToItsAddress()
    {
        AssemblyResult result = new Assembler().assemble("start:\n  JP start");
        Assert.Equal(0x200, result.symbols["start"]);
        Assert.Equal(new byte[] { 0x12, 0x00 }, result.rom);
    }

    [Fact]
    public void forwardLabelIsResolvedInTwoPasses()
    {
        byte[] rom = assemble("  JP end\n  LD V0, 1\nend:\n  RET");
        Assert.Equal(new byte[] { 0x12, 0x04, 0x60, 0x01, 0x00, 0xEE }, rom);
    }

    [Fact]
    public void macroExpandsWithArguments()
    {
        string source = "MACRO SETV reg, val\n  LD reg, val\nENDM\nSETV V3, 0x0A";
        Assert.Equal(new byte[] { 0x63, 0x0A }, assemble(source));
    }

    [Fact]
    public void listingContainsAddressesAndMnemonics()
    {
        AssemblyResult result = new Assembler().assemble("LD V0, 1\nRET");
        Assert.Contains("0x0200:", result.listing);
        Assert.Contains("LD V0, 0x01", result.listing);
        Assert.Contains("RET", result.listing);
    }

    [Theory]
    [InlineData("FOO V0")]
    [InlineData("LD V0, missing")]
    [InlineData("a:\na:")]
    [InlineData("LD V0, 0x1FF")]
    [InlineData("DRW V0, V1, 0x20")]
    public void rejectsMalformedProgram(string source)
    {
        Assert.Throws<AsmException>(() => assemble(source));
    }

    [Fact]
    public void roundTripCoreProgramSurvivesDisassembly()
    {
        assertRoundTrip(string.Join('\n', new[]
        {
            "CLS", "RET", "JP 0x2A0", "CALL 0x2B0",
            "SE V0, 0x10", "SNE V1, 0x20", "SE V2, V3", "SNE V4, V5",
            "LD V6, 0x7F", "ADD V7, 0x01", "OR V8, V9", "AND VA, VB",
            "XOR VC, VD", "ADD VE, VF", "SUB V0, V1", "SUBN V2, V3",
            "SHR V4", "SHL V5", "LD I, 0x300", "JP V0, 0x2C0",
            "RND V6, 0x0F", "DRW V0, V1, 5", "SKP V2", "SKNP V3",
            "LD V4, DT", "LD V5, K", "LD DT, V6", "LD ST, V7",
            "ADD I, V8", "LD F, V9", "LD B, VA", "LD [I], VB", "LD VC, [I]",
        }));
    }

    [Fact]
    public void roundTripExtendedProgramSurvivesDisassembly()
    {
        assertRoundTrip(string.Join('\n', new[]
        {
            "SCD 6", "SCU 3", "SCR", "SCL", "HIGH", "LOW",
            "PLANE 3", "AUDIO", "SAVE V0-V5", "LOAD V2-V4",
            "LD HF, V5", "PITCH V2", "LD R, V3", "LD V3, R", "LD I, 0x1234",
        }));
    }

    static void assertRoundTrip(string source)
    {
        byte[] first = assemble(source);

        var disassembled = new List<string>();
        foreach (DecodedInstruction line in Disassembler.disassemble(first, 0x200))
        {
            disassembled.Add(line.mnemonic);
        }

        byte[] second = assemble(string.Join('\n', disassembled));
        Assert.Equal(first, second);
    }
}
