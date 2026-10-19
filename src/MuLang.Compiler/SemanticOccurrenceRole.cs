namespace MuLang.Compiler;

/// <summary>Specifies how a semantic symbol occurrence is used.</summary>
public enum SemanticOccurrenceRole
{
    /// <summary>Identifies a symbol declaration.</summary>
    Declaration,

    /// <summary>Identifies a value read.</summary>
    Read,

    /// <summary>Identifies a value write or removal.</summary>
    Write,

    /// <summary>Identifies a reference without read or write semantics.</summary>
    Reference,
}
