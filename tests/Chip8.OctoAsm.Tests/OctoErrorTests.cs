using Chip8.OctoAsm;
using Xunit;

namespace Chip8.OctoAsm.Tests;

public class OctoErrorTests
{
    static OctoCompileException fail(string source) =>
        Assert.Throws<OctoCompileException>(() => OctoAssembler.assemble(source));

    [Theory]
    [InlineData(": main jump nowhere", "undefined name 'nowhere'")]
    [InlineData(": start clear", "program has no 'main' label")]
    [InlineData(": main loop clear", "'loop' is never closed")]
    [InlineData(": main if v0 == 1 begin clear", "'if' is never closed")]
    [InlineData(": main else", "'else' without a matching 'if ... begin'")]
    [InlineData(": main again", "'again' without a matching 'loop'")]
    [InlineData(": main then", "'then' without a matching 'if'")]
    [InlineData(": main if v0 == 1 clear", "expected 'then' or 'begin'")]
    [InlineData(": main if v0 ~ 1 then clear", "unknown comparison")]
    [InlineData(": main while v0 == 1", "'while' must appear directly inside a 'loop'")]
    [InlineData(": main v0 := 256", "does not fit in a byte")]
    [InlineData(": main v0 := -129", "does not fit in a byte")]
    [InlineData(": main sprite v0 v1 16", "does not fit in a nibble")]
    [InlineData(": main plane 4", "plane mask must be 0..3")]
    [InlineData(": main v0 ** 3", "unknown register operator")]
    [InlineData(": main i -= v0", "expected ':=' or '+=' after 'i'")]
    [InlineData(": main sprite v0 7 3", "expected a register")]
    [InlineData(": main :stringmode", "unsupported directive ':stringmode'")]
    [InlineData(": main : main", "'main' is already defined")]
    [InlineData(":const X 1 :const X 2 : main", "'X' is already defined")]
    [InlineData(": main : v3", "expected a name")]
    [InlineData(": main jump v3", "expected an address or label")]
    [InlineData(": main if vf < 3 then clear", "vf cannot be compared")]
    [InlineData(": main :assert { 1 == 2 }", "assertion failed")]
    [InlineData(": main :calc x { missing + 1 }", "undefined name 'missing' in expression")]
    [InlineData(": main :org 0x100", "outside 0x200..0xFFFF")]
    [InlineData(": main jump far :org 0x1000 : far clear", "beyond the 12-bit address range")]
    [InlineData(":macro forever { forever } : main forever", "macro expansion limit")]
    [InlineData(":macro pair A B { v0 := A } : main pair 1", "expects 2 argument(s)")]
    [InlineData(":macro broken { clear", "missing its closing '}'")]
    [InlineData(": main }", "unexpected '}'")]
    [InlineData(": main loop", "'loop' is never closed")]
    public void rejectsInvalidPrograms(string source, string message)
    {
        Assert.Contains(message, fail(source).Message);
    }

    [Fact]
    public void diagnosticsCarryLineAndColumn()
    {
        OctoCompileException error = fail(": main\n\n  v0 := 300");

        Assert.Equal(3, error.diagnostic.line);
        Assert.Equal(9, error.diagnostic.column);
        Assert.StartsWith("error at line 3, col 9:", error.diagnostic.ToString());
    }

    [Fact]
    public void undefinedNameReportsTheUseSite()
    {
        OctoCompileException error = fail(": main\n  clear\n  jump later-label");

        Assert.Equal(3, error.diagnostic.line);
        Assert.Equal(8, error.diagnostic.column);
    }
}
