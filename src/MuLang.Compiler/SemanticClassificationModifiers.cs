namespace MuLang.Compiler;

/// <summary>Specifies modifiers for a binding-based semantic source classification.</summary>
[Flags]
public enum SemanticClassificationModifiers
{
    /// <summary>Specifies no semantic modifier.</summary>
    None = 0,

    /// <summary>Identifies a symbol declaration.</summary>
    Declaration = 1,

    /// <summary>Identifies a symbol that cannot be assigned by MuLang source code.</summary>
    ReadOnly = 2,

    /// <summary>Identifies a symbol supplied by the host environment.</summary>
    DefaultLibrary = 4,
}
