using MuLang.Core.Text;

namespace MuLang.IR;

/// <summary>Represents diagnostic attributes associated with an IR function.</summary>
/// <param name="SourceName">The source-level function name, when available.</param>
/// <param name="QualifiedSourceName">The qualified source-level function name, when available.</param>
/// <param name="DeclarationSpan">The source declaration span, when available.</param>
public sealed record IrFunctionAttributes(
    string? SourceName,
    string? QualifiedSourceName,
    TextSpan? DeclarationSpan
);
