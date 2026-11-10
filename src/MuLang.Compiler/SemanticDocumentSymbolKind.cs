namespace MuLang.Compiler;

/// <summary>Specifies the kind of a source declaration in a document outline.</summary>
public enum SemanticDocumentSymbolKind
{
    /// <summary>Identifies a type declaration.</summary>
    Type,

    /// <summary>Identifies a function declaration.</summary>
    Function,

    /// <summary>Identifies a property declaration.</summary>
    Property,

    /// <summary>Identifies a function parameter.</summary>
    Parameter,

    /// <summary>Identifies a variable declaration or catch binding.</summary>
    Variable,
}
