namespace MuLang.Compiler;

/// <summary>Specifies a binding-based semantic source classification.</summary>
public enum SemanticClassificationKind
{
    /// <summary>Identifies a named type.</summary>
    Type,

    /// <summary>Identifies a function.</summary>
    Function,

    /// <summary>Identifies a user-function parameter.</summary>
    Parameter,

    /// <summary>Identifies a local or global variable.</summary>
    Variable,

    /// <summary>Identifies an object property.</summary>
    Property,
}
