using System.Collections.Generic;

namespace Chip8.Core.Assembly;

public abstract class Statement
{
    protected Statement(SourcePosition position)
    {
        this.position = position;
    }

    public SourcePosition position { get; }
}

public sealed class LabelStatement : Statement
{
    public LabelStatement(string name, SourcePosition position) : base(position)
    {
        this.name = name;
    }

    public string name { get; }
}

public sealed class ConstantStatement : Statement
{
    public ConstantStatement(string name, IReadOnlyList<Token> expression, SourcePosition position) : base(position)
    {
        this.name = name;
        this.expression = expression;
    }

    public string name { get; }
    public IReadOnlyList<Token> expression { get; }
}

public sealed class OrgStatement : Statement
{
    public OrgStatement(IReadOnlyList<Token> expression, SourcePosition position) : base(position)
    {
        this.expression = expression;
    }

    public IReadOnlyList<Token> expression { get; }
}

public sealed class AlignStatement : Statement
{
    public AlignStatement(IReadOnlyList<Token> expression, SourcePosition position) : base(position)
    {
        this.expression = expression;
    }

    public IReadOnlyList<Token> expression { get; }
}

public sealed class DataStatement : Statement
{
    public DataStatement(bool word, IReadOnlyList<IReadOnlyList<Token>> items, SourcePosition position) : base(position)
    {
        this.word = word;
        this.items = items;
    }

    public bool word { get; }
    public IReadOnlyList<IReadOnlyList<Token>> items { get; }
}

public sealed class InstructionStatement : Statement
{
    public InstructionStatement(string mnemonic, IReadOnlyList<Operand> operands, SourcePosition position) : base(position)
    {
        this.mnemonic = mnemonic;
        this.operands = operands;
    }

    public string mnemonic { get; }
    public IReadOnlyList<Operand> operands { get; }
}
