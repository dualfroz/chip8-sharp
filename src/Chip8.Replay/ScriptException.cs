using System;

namespace Chip8.Replay;

public class ScriptException : Exception
{
    public int line { get; }

    public ScriptException(int line, string message)
        : base("line " + line + ": " + message)
    {
        this.line = line;
    }
}

public sealed class ScriptParseException : ScriptException
{
    public ScriptParseException(int line, string message)
        : base(line, message)
    {
    }
}

public sealed class ScriptAssertionException : ScriptException
{
    public int frame { get; }

    public ScriptAssertionException(int line, int frame, string message)
        : base(line, "frame " + frame + ": " + message)
    {
        this.frame = frame;
    }
}
