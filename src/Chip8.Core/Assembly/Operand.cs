using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public enum OperandKind
{
    Register,
    RegisterRange,
    Immediate,
    IndexRegister,
    IndexIndirect,
    DelayTimer,
    SoundTimer,
    KeyReg,
    FontReg,
    BigFontReg,
    BcdReg,
    RplReg,
}

public sealed class Operand
{
    Operand(OperandKind kind, SourcePosition position)
    {
        this.kind = kind;
        this.position = position;
    }

    public OperandKind kind { get; }
    public SourcePosition position { get; }
    public int registerIndex { get; private init; }
    public int rangeEnd { get; private init; }
    public IReadOnlyList<Token> expressionTokens { get; private init; } = System.Array.Empty<Token>();

    public static Operand register(int index, SourcePosition position) =>
        new(OperandKind.Register, position) { registerIndex = index };

    public static Operand registerRange(int start, int end, SourcePosition position) =>
        new(OperandKind.RegisterRange, position) { registerIndex = start, rangeEnd = end };

    public static Operand immediate(IReadOnlyList<Token> tokens, SourcePosition position) =>
        new(OperandKind.Immediate, position) { expressionTokens = tokens };

    public static Operand special(OperandKind kind, SourcePosition position) => new(kind, position);

    public bool isRegister(int index) => kind == OperandKind.Register && registerIndex == index;

    public override string ToString() => kind switch
    {
        OperandKind.Register => "V" + registerIndex.ToString("X1"),
        OperandKind.RegisterRange => "V" + registerIndex.ToString("X1") + "-V" + rangeEnd.ToString("X1"),
        OperandKind.Immediate => "imm",
        OperandKind.IndexIndirect => "[I]",
        _ => kind.ToString(),
    };
}
