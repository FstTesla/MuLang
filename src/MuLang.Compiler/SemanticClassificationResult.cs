using MuLang.Core.Diagnostics;

namespace MuLang.Compiler;

/// <summary>Represents binding-based semantic classifications and diagnostics for MuLang source code.</summary>
public sealed class SemanticClassificationResult
{
    internal SemanticClassificationResult(
        IReadOnlyList<SemanticClassification> classifications,
        DiagnosticCollection diagnostics
    )
    {
        Classifications = classifications;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the semantic source classifications.</summary>
    public IReadOnlyList<SemanticClassification> Classifications { get; }

    /// <summary>Gets the diagnostics produced while classifying the source.</summary>
    public DiagnosticCollection Diagnostics { get; }
}
