using System;
using Chip8.OctoAsm;
using Xunit;

namespace Chip8.OctoAsm.Tests;

public class OctoEncodingTests
{
    static string assembleHex(string body) =>
        Convert.ToHexString(OctoAssembler.assembleRom(": main\n" + body));

    [Theory]
    [InlineData("clear", "00E0")]
    [InlineData("return", "00EE")]
    [InlineData(";", "00EE")]
    [InlineData("jump 0x300", "1300")]
    [InlineData("jump0 0x300", "B300")]
    [InlineData("native 0x123", "0123")]
    [InlineData("bcd v4", "F433")]
    [InlineData("save v5", "F555")]
    [InlineData("load vA", "FA65")]
    [InlineData("sprite v1 v2 5", "D125")]
    [InlineData("delay := v3", "F315")]
    [InlineData("buzzer := v3", "F318")]
    [InlineData("i := 0x345", "A345")]
    [InlineData("i := hex v9", "F929")]
    [InlineData("i += v2", "F21E")]
    [InlineData("v3 := 0x42", "6342")]
    [InlineData("v3 := -1", "63FF")]
    [InlineData("vf := 0b101", "6F05")]
    [InlineData("v3 := v4", "8340")]
    [InlineData("v3 := random 0x0F", "C30F")]
    [InlineData("v3 := key", "F30A")]
    [InlineData("v3 := delay", "F307")]
    [InlineData("v3 += 7", "7307")]
    [InlineData("v3 += v4", "8344")]
    [InlineData("v3 -= 1", "73FF")]
    [InlineData("v3 -= v4", "8345")]
    [InlineData("v3 =- v4", "8347")]
    [InlineData("v3 |= v4", "8341")]
    [InlineData("v3 &= v4", "8342")]
    [InlineData("v3 ^= v4", "8343")]
    [InlineData("v3 >>= v4", "8346")]
    [InlineData("v3 <<= v4", "834E")]
    public void encodesChip8Statements(string source, string expected)
    {
        Assert.Equal(expected, assembleHex(source));
    }

    [Theory]
    [InlineData("hires", "00FF")]
    [InlineData("lores", "00FE")]
    [InlineData("exit", "00FD")]
    [InlineData("scroll-left", "00FC")]
    [InlineData("scroll-right", "00FB")]
    [InlineData("scroll-down 3", "00C3")]
    [InlineData("i := bighex v9", "F930")]
    [InlineData("saveflags v7", "F775")]
    [InlineData("loadflags v7", "F785")]
    [InlineData("sprite v1 v2 0", "D120")]
    public void encodesSuperChipStatements(string source, string expected)
    {
        Assert.Equal(expected, assembleHex(source));
    }

    [Theory]
    [InlineData("scroll-up 2", "00D2")]
    [InlineData("plane 3", "F301")]
    [InlineData("audio", "F002")]
    [InlineData("pitch := v3", "F33A")]
    [InlineData("save v2 - v6", "5262")]
    [InlineData("load v6 - v2", "5623")]
    [InlineData("i := long 0x1234", "F0001234")]
    public void encodesXoChipStatements(string source, string expected)
    {
        Assert.Equal(expected, assembleHex(source));
    }

    [Theory]
    [InlineData("if v1 == 3 then clear", "410300E0")]
    [InlineData("if v1 != 3 then clear", "310300E0")]
    [InlineData("if v1 == v2 then clear", "912000E0")]
    [InlineData("if v1 != v2 then clear", "512000E0")]
    [InlineData("if v1 key then clear", "E1A100E0")]
    [InlineData("if v1 -key then clear", "E19E00E0")]
    [InlineData("if v1 >= 9 then clear", "6F098F174F0100E0")]
    [InlineData("if v1 < v2 then clear", "8F208F174F0000E0")]
    public void encodesConditionalSkips(string source, string expected)
    {
        Assert.Equal(expected, assembleHex(source));
    }

    [Theory]
    [InlineData("0x12 0x34 255", "1234FF")]
    [InlineData(":byte { 3 * 4 }", "0C")]
    [InlineData(":byte -2", "FE")]
    [InlineData(":const SIZE 9 v0 := SIZE SIZE", "600909")]
    [InlineData(":calc DOUBLE { 2 * 0x10 } v0 := DOUBLE", "6020")]
    [InlineData(":alias counter v7 counter := 3 counter += 1", "67037701")]
    public void encodesDataAndNamedValues(string source, string expected)
    {
        Assert.Equal(expected, assembleHex(source));
    }
}
