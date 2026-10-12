using MuLang.Core.Text;

namespace MuLang.IR;

/// <summary>Represents diagnostic attributes associated with an IR slot.</summary>
/// <param name="SourceName">The source-level slot name, when available.</param>
/// <param name="DeclarationSpan">The source declaration span, when available.</param>
public sealed record IrSlotAttributes(
    string? SourceName,
    TextSpan? DeclarationSpan
);
