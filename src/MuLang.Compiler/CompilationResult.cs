using MuLang.Core.Diagnostics;
using MuLang.IR;

namespace MuLang.Compiler;

/// <summary>Represents the result of compiling MuLang source code to portable IR.</summary>
public sealed class CompilationResult
{
    internal CompilationResult(
        IrProgram? program,
        DiagnosticCollection diagnostics
    )
    {
        Program = program;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the compiled portable IR program, or <c>null</c> when compilation failed.</summary>
    public IrProgram? Program { get; }

    /// <summary>Gets the diagnostics produced by compilation.</summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>Gets a value indicating whether compilation produced valid portable IR without errors.</summary>
    public bool IsSuccessful => Program is not null && !Diagnostics.HasErrors;
}
