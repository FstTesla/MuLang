using MuLang.Core.Diagnostics;

namespace MuLang.Compiler;

/// <summary>Represents lexical classifications and diagnostics for MuLang source code.</summary>
public sealed class ClassificationResult
{
    internal ClassificationResult(
        IReadOnlyList<SourceClassification> classifications,
        DiagnosticCollection diagnostics
    )
    {
        Classifications = classifications;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the lexical source classifications.</summary>
    public IReadOnlyList<SourceClassification> Classifications { get; }

    /// <summary>Gets the diagnostics produced while classifying the source.</summary>
    public DiagnosticCollection Diagnostics { get; }
}
