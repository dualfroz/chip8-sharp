using System.Collections.Generic;

namespace Chip8.OctoAsm;

public sealed partial class OctoAssembler
{
    enum FrameKind
    {
        If,
        Else,
        Loop,
    }

    sealed class ControlFrame
    {
        public ControlFrame(FrameKind kind, string keyword, SourcePosition position, int address)
        {
            this.kind = kind;
            this.keyword = keyword;
            this.position = position;
            this.address = address;
        }

        public FrameKind kind { get; }
        public string keyword { get; }
        public SourcePosition position { get; }
        public int address { get; }
        public List<int> exitJumps { get; } = new();
    }

    readonly struct Condition
    {
        public Condition(int register, string op, bool rightIsRegister, int right, SourcePosition position)
        {
            this.register = register;
            this.op = op;
            this.rightIsRegister = rightIsRegister;
            this.right = right;
            this.position = position;
        }

        public int register { get; }
        public string op { get; }
        public bool rightIsRegister { get; }
        public int right { get; }
        public SourcePosition position { get; }
    }

    readonly Stack<ControlFrame> controlStack = new();

    bool controlStatement(OctoToken token)
    {
        SourcePosition where = token.position;
        switch (token.text)
        {
            case "if":
                ifStatement(where);
                return true;

            case "else":
            {
                ControlFrame frame = popFrame(token, FrameKind.If);
                int jumpAt = emitPlaceholderJump(where);
                patchJump(frame.address, image.here, where);
                controlStack.Push(new ControlFrame(FrameKind.Else, "else", where, jumpAt));
                return true;
            }

            case "end":
            {
                ControlFrame frame = popFrame(token, FrameKind.If, FrameKind.Else);
                patchJump(frame.address, image.here, where);
                return true;
            }

            case "loop":
                ensureEntry(where);
                controlStack.Push(new ControlFrame(FrameKind.Loop, "loop", where, image.here));
                return true;

            case "while":
            {
                if (controlStack.Count == 0 || controlStack.Peek().kind != FrameKind.Loop)
                {
                    throw error("'while' must appear directly inside a 'loop'", where);
                }
                ControlFrame loop = controlStack.Peek();
                emitSkip(parseCondition(), true);
                loop.exitJumps.Add(emitPlaceholderJump(where));
                return true;
            }

            case "again":
            {
                ControlFrame loop = popFrame(token, FrameKind.Loop);
                emitInstruction(0x1000 | checkAddress12(loop.address, "loop", where), where);
                foreach (int exit in loop.exitJumps)
                {
                    patchJump(exit, image.here, where);
                }
                return true;
            }

            case "then":
            case "begin":
                throw error("'" + token.text + "' without a matching 'if'", where);

            default:
                return false;
        }
    }

    void ifStatement(SourcePosition where)
    {
        Condition condition = parseCondition();
        OctoToken mode = next();

        if (mode.isWord("then"))
        {
            emitSkip(condition, false);
            return;
        }

        if (mode.isWord("begin"))
        {
            emitSkip(condition, true);
            int jumpAt = emitPlaceholderJump(where);
            controlStack.Push(new ControlFrame(FrameKind.If, "if", where, jumpAt));
            return;
        }

        throw error("expected 'then' or 'begin' after the condition but found " + mode, mode.position);
    }

    Condition parseCondition()
    {
        OctoToken left = next();
        int register = registerIndex(left);
        OctoToken op = next();

        switch (op.text)
        {
            case "key":
            case "-key":
                return new Condition(register, op.text, false, 0, left.position);

            case "==":
            case "!=":
            case "<":
            case ">":
            case "<=":
            case ">=":
            {
                OctoToken right = next();
                return isRegister(right)
                    ? new Condition(register, op.text, true, registerIndex(right), left.position)
                    : new Condition(register, op.text, false, byteOperand(right), left.position);
            }

            default:
                throw error("unknown comparison " + op, op.position);
        }
    }

    void emitSkip(Condition condition, bool skipWhenTrue)
    {
        int x = condition.register;
        SourcePosition where = condition.position;

        switch (condition.op)
        {
            case "==":
                emitEqualitySkip(condition, skipWhenTrue);
                return;
            case "!=":
                emitEqualitySkip(condition, !skipWhenTrue);
                return;
            case "key":
                emitInstruction((skipWhenTrue ? 0xE09E : 0xE0A1) | (x << 8), where);
                return;
            case "-key":
                emitInstruction((skipWhenTrue ? 0xE0A1 : 0xE09E) | (x << 8), where);
                return;
            default:
                emitRelationalSkip(condition, skipWhenTrue);
                return;
        }
    }

    void emitEqualitySkip(Condition condition, bool skipIfEqual)
    {
        int x = condition.register << 8;
        if (condition.rightIsRegister)
        {
            emitInstruction((skipIfEqual ? 0x5000 : 0x9000) | x | (condition.right << 4), condition.position);
        }
        else
        {
            emitInstruction((skipIfEqual ? 0x3000 : 0x4000) | x | condition.right, condition.position);
        }
    }

    void emitRelationalSkip(Condition condition, bool skipWhenTrue)
    {
        SourcePosition where = condition.position;
        if (condition.register == 0xF || (condition.rightIsRegister && condition.right == 0xF))
        {
            throw error("vf cannot be compared with '" + condition.op + "' because the comparison uses vf as scratch", where);
        }

        int x = condition.register;
        int y = condition.right;
        bool bIsRegister = condition.rightIsRegister;
        int a;
        int b;
        int trueFlag;

        switch (condition.op)
        {
            case ">=":
                a = x;
                b = y;
                trueFlag = 1;
                break;
            case "<":
                a = x;
                b = y;
                trueFlag = 0;
                break;
            case "<=" when bIsRegister:
                a = y;
                b = x;
                trueFlag = 1;
                break;
            case "<=":
                if (y == 0xFF)
                {
                    emitConstantSkip(skipWhenTrue, where);
                    return;
                }
                a = x;
                b = y + 1;
                trueFlag = 0;
                break;
            case ">" when bIsRegister:
                a = y;
                b = x;
                trueFlag = 0;
                break;
            default:
                if (y == 0xFF)
                {
                    emitConstantSkip(!skipWhenTrue, where);
                    return;
                }
                a = x;
                b = y + 1;
                trueFlag = 1;
                break;
        }

        emitInstruction(bIsRegister ? 0x8F00 | (b << 4) : 0x6F00 | b, where);
        emitInstruction(0x8F07 | (a << 4), where);
        emitInstruction((skipWhenTrue ? 0x3F00 : 0x4F00) | trueFlag, where);
    }

    void emitConstantSkip(bool skip, SourcePosition where)
    {
        emitInstruction(0x6F00, where);
        emitInstruction(skip ? 0x3F00 : 0x4F00, where);
    }

    int emitPlaceholderJump(SourcePosition where)
    {
        ensureEntry(where);
        int address = image.here;
        emitInstruction(0x1000, where);
        return address;
    }

    void patchJump(int address, int target, SourcePosition where)
    {
        image.patchAddress(address, checkAddress12(target, "jump target", where));
    }

    ControlFrame popFrame(OctoToken token, params FrameKind[] accepted)
    {
        if (controlStack.Count > 0)
        {
            ControlFrame frame = controlStack.Peek();
            foreach (FrameKind kind in accepted)
            {
                if (frame.kind == kind)
                {
                    return controlStack.Pop();
                }
            }
        }
        throw error("'" + token.text + "' without a matching " + (accepted[0] == FrameKind.Loop ? "'loop'" : "'if ... begin'"), token.position);
    }
}
