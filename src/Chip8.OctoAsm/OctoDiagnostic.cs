using System;
using System.Collections.Generic;

namespace Chip8.OctoAsm;

public enum OctoSeverity
{
    Error,
    Warning,
}

public readonly struct OctoDiagnostic
{
    public OctoDiagnostic(OctoSeverity severity, string message, SourcePosition position)
    {
        this.severity = severity;
        this.message = message;
        this.position = position;
    }

    public OctoSeverity severity { get; }
    public string message { get; }
    public SourcePosition position { get; }

    public int line => position.line;
    public int column => position.column;

    public override string ToString() =>
        severity.ToString().ToLowerInvariant() + " at " + position + ": " + message;
}

public sealed class OctoCompileException : Exception
{
    public OctoCompileException(OctoDiagnostic diagnostic)
        : this(new[] { diagnostic })
    {
    }

    public OctoCompileException(IReadOnlyList<OctoDiagnostic> diagnostics)
        : base(describe(diagnostics))
    {
        this.diagnostics = diagnostics;
    }

    public IReadOnlyList<OctoDiagnostic> diagnostics { get; }

    public OctoDiagnostic diagnostic => diagnostics[0];

    static string describe(IReadOnlyList<OctoDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 1)
        {
            return diagnostics[0].ToString();
        }
        return diagnostics.Count + " Octo compile errors; first: " + diagnostics[0];
    }
}
