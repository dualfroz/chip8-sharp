using System.Globalization;

namespace Chip8.Analysis;

public sealed class InstructionInfo
{
    public InstructionInfo(
        ushort address,
        ushort opcode,
        ushort? extraWord,
        int byteLength,
        InstructionKind kind,
        InstructionCategory category,
        FlowEffect flow,
        string mnemonic)
    {
        this.address = address;
        this.opcode = opcode;
        this.extraWord = extraWord;
        this.byteLength = byteLength;
        this.kind = kind;
        this.category = category;
        this.flow = flow;
        this.mnemonic = mnemonic;
        fields = new OpcodeFields(opcode);
    }

    public ushort address { get; }
    public ushort opcode { get; }
    public ushort? extraWord { get; }
    public int byteLength { get; }
    public InstructionKind kind { get; }
    public InstructionCategory category { get; }
    public FlowEffect flow { get; }
    public string mnemonic { get; }
    public OpcodeFields fields { get; }

    public int x => fields.x;
    public int y => fields.y;
    public int n => fields.n;
    public int nn => fields.nn;
    public ushort nnn => fields.nnn;

    public ushort nextAddress => (ushort)(address + byteLength);

    public bool isTerminator =>
        flow is FlowEffect.Jump
            or FlowEffect.Branch
            or FlowEffect.Call
            or FlowEffect.Return
            or FlowEffect.IndirectJump
            or FlowEffect.Terminate;

    public bool isBranch => flow == FlowEffect.Branch;

    public bool fallsThrough => flow is FlowEffect.Fallthrough or FlowEffect.Branch or FlowEffect.Call;

    public bool hasStaticTarget => flow is FlowEffect.Jump or FlowEffect.Call;

    public bool writesMemory =>
        kind is InstructionKind.StoreRegisters
            or InstructionKind.StoreBcd
            or InstructionKind.SaveRange;

    public bool readsMemory =>
        kind is InstructionKind.LoadRegisters or InstructionKind.LoadRange;

    public bool loadsIndex => kind is InstructionKind.LoadIndex or InstructionKind.LoadLong;

    public ushort? jumpTarget => kind == InstructionKind.Jump ? nnn : null;

    public ushort? callTarget => kind == InstructionKind.Call ? nnn : null;

    public ushort? indexValue
    {
        get
        {
            if (kind == InstructionKind.LoadIndex)
            {
                return nnn;
            }
            if (kind == InstructionKind.LoadLong)
            {
                return extraWord;
            }
            return null;
        }
    }

    public int? spriteRows => kind == InstructionKind.Draw ? (n == 0 ? 16 : n) : null;

    public override string ToString()
    {
        string body = extraWord.HasValue
            ? opcode.ToString("X4", CultureInfo.InvariantCulture) + " " + extraWord.Value.ToString("X4", CultureInfo.InvariantCulture)
            : opcode.ToString("X4", CultureInfo.InvariantCulture);
        return "0x" + address.ToString("X4", CultureInfo.InvariantCulture)
            + "  " + body.PadRight(9)
            + "  " + mnemonic.PadRight(18)
            + "  [" + kind + "/" + flow + "]";
    }
}
