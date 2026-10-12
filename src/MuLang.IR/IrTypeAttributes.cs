namespace MuLang.IR;

/// <summary>Represents diagnostic attributes associated with an IR type.</summary>
/// <param name="SourceNames">The ordered source-level type aliases.</param>
public sealed record IrTypeAttributes(IReadOnlyList<string> SourceNames);
