using System.Linq;
using Xunit;

namespace Chip8.Analysis.Tests;

public class InstructionDecoderTests
{
    [Theory]
    [InlineData(0x00E0, InstructionKind.ClearScreen, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00EE, InstructionKind.Return, FlowEffect.Return, InstructionCategory.Control)]
    [InlineData(0x00C3, InstructionKind.ScrollDown, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00D2, InstructionKind.ScrollUp, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00FB, InstructionKind.ScrollRight, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00FC, InstructionKind.ScrollLeft, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00FD, InstructionKind.Exit, FlowEffect.Terminate, InstructionCategory.Control)]
    [InlineData(0x00FE, InstructionKind.LowRes, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x00FF, InstructionKind.HiRes, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0x0123, InstructionKind.System, FlowEffect.Fallthrough, InstructionCategory.Control)]
    [InlineData(0x1234, InstructionKind.Jump, FlowEffect.Jump, InstructionCategory.Control)]
    [InlineData(0x2345, InstructionKind.Call, FlowEffect.Call, InstructionCategory.Control)]
    [InlineData(0x3012, InstructionKind.SkipEqualImmediate, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0x4012, InstructionKind.SkipNotEqualImmediate, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0x5120, InstructionKind.SkipEqualRegister, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0x5122, InstructionKind.SaveRange, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0x5123, InstructionKind.LoadRange, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0x5121, InstructionKind.Data, FlowEffect.Fallthrough, InstructionCategory.Data)]
    [InlineData(0x6012, InstructionKind.LoadImmediate, FlowEffect.Fallthrough, InstructionCategory.Arithmetic)]
    [InlineData(0x7012, InstructionKind.AddImmediate, FlowEffect.Fallthrough, InstructionCategory.Arithmetic)]
    [InlineData(0x8124, InstructionKind.Arithmetic, FlowEffect.Fallthrough, InstructionCategory.Arithmetic)]
    [InlineData(0x8128, InstructionKind.Data, FlowEffect.Fallthrough, InstructionCategory.Data)]
    [InlineData(0x9120, InstructionKind.SkipNotEqualRegister, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0x9121, InstructionKind.Data, FlowEffect.Fallthrough, InstructionCategory.Data)]
    [InlineData(0xA123, InstructionKind.LoadIndex, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xB123, InstructionKind.JumpWithOffset, FlowEffect.IndirectJump, InstructionCategory.Control)]
    [InlineData(0xC1FF, InstructionKind.Random, FlowEffect.Fallthrough, InstructionCategory.Arithmetic)]
    [InlineData(0xD125, InstructionKind.Draw, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0xE19E, InstructionKind.SkipKeyPressed, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0xE1A1, InstructionKind.SkipKeyReleased, FlowEffect.Branch, InstructionCategory.Skip)]
    [InlineData(0xE100, InstructionKind.Data, FlowEffect.Fallthrough, InstructionCategory.Data)]
    [InlineData(0xF107, InstructionKind.LoadDelay, FlowEffect.Fallthrough, InstructionCategory.Timer)]
    [InlineData(0xF10A, InstructionKind.WaitKey, FlowEffect.Fallthrough, InstructionCategory.Io)]
    [InlineData(0xF115, InstructionKind.SetDelay, FlowEffect.Fallthrough, InstructionCategory.Timer)]
    [InlineData(0xF118, InstructionKind.SetSound, FlowEffect.Fallthrough, InstructionCategory.Sound)]
    [InlineData(0xF11E, InstructionKind.AddIndex, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF129, InstructionKind.LoadFont, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF130, InstructionKind.LoadBigFont, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF133, InstructionKind.StoreBcd, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF13A, InstructionKind.Pitch, FlowEffect.Fallthrough, InstructionCategory.Sound)]
    [InlineData(0xF155, InstructionKind.StoreRegisters, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF165, InstructionKind.LoadRegisters, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF175, InstructionKind.StoreFlags, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF185, InstructionKind.LoadFlags, FlowEffect.Fallthrough, InstructionCategory.Memory)]
    [InlineData(0xF201, InstructionKind.Plane, FlowEffect.Fallthrough, InstructionCategory.Draw)]
    [InlineData(0xF002, InstructionKind.Audio, FlowEffect.Fallthrough, InstructionCategory.Sound)]
    [InlineData(0xF1FF, InstructionKind.Data, FlowEffect.Fallthrough, InstructionCategory.Data)]
    public void classifiesEveryOpcodeFamily(int opcode, InstructionKind kind, FlowEffect flow, InstructionCategory category)
    {
        InstructionInfo info = InstructionDecoder.decode(0x200, (ushort)opcode, null, 2);

        Assert.Equal(kind, info.kind);
        Assert.Equal(flow, info.flow);
        Assert.Equal(category, info.category);
    }

    [Fact]
    public void longIndexLoadIsFourBytes()
    {
        byte[] program = { 0xF0, 0x00, 0x12, 0x34, 0x00, 0xE0 };

        InstructionInfo info = InstructionDecoder.decodeAt(program, 0x200)!;
        Assert.Equal(InstructionKind.LoadLong, info.kind);
        Assert.Equal(4, info.byteLength);
        Assert.Equal((ushort)0x1234, info.indexValue);
        Assert.Equal(0x204, info.nextAddress);
        Assert.True(info.loadsIndex);
        Assert.Equal(4, InstructionDecoder.byteLengthAt(program, 0x200));
        Assert.Equal("LD I, 0x1234", info.mnemonic);

        Assert.Equal(new ushort[] { 0x200, 0x204 }, InstructionDecoder.decodeLinear(program).Select(entry => entry.address).ToArray());
    }

    [Fact]
    public void addressesOutsideTheProgramDecodeToNull()
    {
        byte[] program = { 0x00, 0xE0, 0x12 };

        Assert.Null(InstructionDecoder.decodeAt(program, 0x1FE));
        Assert.Null(InstructionDecoder.decodeAt(program, 0x202));
        Assert.Single(InstructionDecoder.decodeLinear(program));
    }

    [Fact]
    public void instructionInfoExposesTargetsAndEffects()
    {
        InstructionInfo jump = InstructionDecoder.decode(0x200, 0x1ABC, null, 2);
        InstructionInfo call = InstructionDecoder.decode(0x200, 0x2ABC, null, 2);
        InstructionInfo draw = InstructionDecoder.decode(0x200, 0xD120, null, 2);
        InstructionInfo store = InstructionDecoder.decode(0x200, 0xF355, null, 2);
        InstructionInfo load = InstructionDecoder.decode(0x200, 0x5233, null, 2);

        Assert.Equal((ushort)0xABC, jump.jumpTarget);
        Assert.Null(jump.callTarget);
        Assert.Equal((ushort)0xABC, call.callTarget);
        Assert.True(call.isTerminator && call.fallsThrough && call.hasStaticTarget);
        Assert.Equal(16, draw.spriteRows);
        Assert.False(draw.isTerminator);
        Assert.True(store.writesMemory);
        Assert.True(load.readsMemory);
        Assert.Equal(3, store.x);
        Assert.Equal(0x55, store.nn);
        Assert.Contains("[Call/Call]", call.ToString());
    }

    [Fact]
    public void opcodeFieldsSplitTheWord()
    {
        var fields = new OpcodeFields(0xD3A7);

        Assert.Equal(0xD, fields.highNibble);
        Assert.Equal(3, fields.x);
        Assert.Equal(0xA, fields.y);
        Assert.Equal(7, fields.n);
        Assert.Equal(0xA7, fields.nn);
        Assert.Equal(0x3A7, fields.nnn);
        Assert.Equal("0xD3A7 x=3 y=10 n=7 nn=0xA7 nnn=0x3A7", fields.ToString());
    }
}
