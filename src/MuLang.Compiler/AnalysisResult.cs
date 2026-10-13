using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents the result of analyzing MuLang source code without producing portable IR.</summary>
public sealed class AnalysisResult
{
    internal AnalysisResult(
        DiagnosticCollection diagnostics,
        Uri? sourceDocument,
        TextLineMap lineMap
    )
    {
        Diagnostics = diagnostics;
        SourceDocument = sourceDocument;
        LineMap = lineMap;
    }

    /// <summary>Gets the diagnostics produced by analysis.</summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>Gets the source document URI, when available.</summary>
    public Uri? SourceDocument { get; }

    /// <summary>Gets the source line map.</summary>
    public TextLineMap LineMap { get; }

    /// <summary>Gets a value indicating whether analysis completed without errors.</summary>
    public bool IsSuccessful => !Diagnostics.HasErrors;
}
