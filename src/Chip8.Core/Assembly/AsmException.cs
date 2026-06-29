using System;

namespace Chip8.Core.Assembly;

public sealed class AsmException : Exception
{
    public AsmException(string message, SourcePosition position)
        : base("assembly error at " + position + ": " + message)
    {
        this.reason = message;
        this.position = position;
    }

    public string reason { get; }
    public SourcePosition position { get; }
}
