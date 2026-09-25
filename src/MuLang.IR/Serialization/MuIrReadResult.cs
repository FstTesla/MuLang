namespace MuLang.IR.Serialization;

/// <summary>Represents the result of reading a MuIR document.</summary>
public sealed class MuIrReadResult
{
    internal MuIrReadResult(
        IrProgram? program,
        IReadOnlyList<MuIrDiagnostic> diagnostics
    )
    {
        Program = program;
        Diagnostics = Array.AsReadOnly([ .. diagnostics ]);
    }

    /// <summary>Gets the reconstructed program, or <c>null</c> when reading failed.</summary>
    public IrProgram? Program { get; }

    /// <summary>Gets the ordered reader diagnostics.</summary>
    public IReadOnlyList<MuIrDiagnostic> Diagnostics { get; }

    /// <summary>Gets a value indicating whether reading succeeded.</summary>
    public bool Success => Program is not null && Diagnostics.Count == 0;
}
