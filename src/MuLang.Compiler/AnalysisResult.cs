using MuLang.Core.Diagnostics;

namespace MuLang.Compiler;

/// <summary>Represents the result of analyzing MuLang source code without producing portable IR.</summary>
public sealed class AnalysisResult
{
    internal AnalysisResult(DiagnosticCollection diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the diagnostics produced by analysis.</summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>Gets a value indicating whether analysis completed without errors.</summary>
    public bool IsSuccessful => !Diagnostics.HasErrors;
}
