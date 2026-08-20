using Chip8.Core;
using Xunit;

namespace Chip8.Tests;

public class DisassemblerTests
{
    [Theory]
    [InlineData(0x00E0, "CLS")]
    [InlineData(0x00EE, "RET")]
    [InlineData(0x00C4, "SCD 4")]
    [InlineData(0x00D6, "SCU 6")]
    [InlineData(0x00FB, "SCR")]
    [InlineData(0x00FC, "SCL")]
    [InlineData(0x00FD, "EXIT")]
    [InlineData(0x00FE, "LORES")]
    [InlineData(0x00FF, "HIRES")]
    [InlineData(0x1234, "JP 0x234")]
    [InlineData(0x2ABC, "CALL 0xABC")]
    [InlineData(0x3A20, "SE VA, 0x20")]
    [InlineData(0x4B30, "SNE VB, 0x30")]
    [InlineData(0x5120, "SE V1, V2")]
    [InlineData(0x5232, "SAVE V2-V3")]
    [InlineData(0x5233, "LOAD V2-V3")]
    [InlineData(0x6A55, "LD VA, 0x55")]
    [InlineData(0x7A01, "ADD VA, 0x01")]
    [InlineData(0x8010, "LD V0, V1")]
    [InlineData(0x8014, "ADD V0, V1")]
    [InlineData(0x8016, "SHR V0")]
    [InlineData(0x8017, "SUBN V0, V1")]
    [InlineData(0x801E, "SHL V0")]
    [InlineData(0x9120, "SNE V1, V2")]
    [InlineData(0xA300, "LD I, 0x300")]
    [InlineData(0xB300, "JP V0, 0x300")]
    [InlineData(0xC0FF, "RND V0, 0xFF")]
    [InlineData(0xD125, "DRW V1, V2, 5")]
    [InlineData(0xD120, "DRW V1, V2, 0")]
    [InlineData(0xE09E, "SKP V0")]
    [InlineData(0xE0A1, "SKNP V0")]
    [InlineData(0xF007, "LD V0, DT")]
    [InlineData(0xF00A, "LD V0, K")]
    [InlineData(0xF029, "LD F, V0")]
    [InlineData(0xF030, "LD HF, V0")]
    [InlineData(0xF033, "LD B, V0")]
    [InlineData(0xF055, "LD [I], V0")]
    [InlineData(0xF065, "LD V0, [I]")]
    [InlineData(0xF075, "LD R, V0")]
    [InlineData(0xF085, "LD V0, R")]
    [InlineData(0xF201, "PLANE 2")]
    [InlineData(0xF002, "AUDIO")]
    [InlineData(0xF03A, "PITCH V0")]
    public void describesKnownOpcodes(int opcode, string expected)
    {
        Assert.Equal(expected, Disassembler.describe((ushort)opcode));
    }

    [Fact]
    public void describesLongLoadWithExtraWord()
    {
        Assert.Equal("LD I, 0x1234", Disassembler.describe(0xF000, 0x1234));
    }

    [Fact]
    public void unknownOpcodeRendersAsDataWord()
    {
        Assert.Equal("DW 0x8ABF", Disassembler.describe(0x8ABF));
    }

    [Fact]
    public void disassemblesProgramAndConsumesLongLoadAsFourBytes()
    {
        var program = new byte[] { 0xF0, 0x00, 0x12, 0x34, 0x00, 0xE0 };
        var lines = Disassembler.disassemble(program);

        Assert.Equal(2, lines.Count);

        Assert.Equal(0x200, lines[0].address);
        Assert.Equal(4, lines[0].byteLength);
        Assert.Equal("LD I, 0x1234", lines[0].mnemonic);

        Assert.Equal(0x204, lines[1].address);
        Assert.Equal(2, lines[1].byteLength);
        Assert.Equal("CLS", lines[1].mnemonic);
    }

    [Fact]
    public void renderIncludesAddressAndOpcode()
    {
        var program = new byte[] { 0x12, 0x34 };
        string text = Disassembler.render(program);
        Assert.Contains("0x0200:", text);
        Assert.Contains("1234", text);
        Assert.Contains("JP 0x234", text);
    }
}
