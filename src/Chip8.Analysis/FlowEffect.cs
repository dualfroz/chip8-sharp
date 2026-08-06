namespace Chip8.Analysis;

public enum FlowEffect
{
    Fallthrough,
    Branch,
    Jump,
    Call,
    Return,
    IndirectJump,
    Terminate,
}
