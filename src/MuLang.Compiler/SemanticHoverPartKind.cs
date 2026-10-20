namespace MuLang.Compiler;

/// <summary>Specifies the classification of a semantic hover signature part.</summary>
public enum SemanticHoverPartKind
{
    /// <summary>Identifies unclassified text and whitespace.</summary>
    Text,

    /// <summary>Identifies a language keyword or intrinsic type.</summary>
    Keyword,

    /// <summary>Identifies a named type.</summary>
    Type,

    /// <summary>Identifies a variable name.</summary>
    Variable,

    /// <summary>Identifies a parameter name.</summary>
    Parameter,

    /// <summary>Identifies a function name.</summary>
    Function,

    /// <summary>Identifies an object property name.</summary>
    Property,

    /// <summary>Identifies a string literal.</summary>
    String,

    /// <summary>Identifies a numeric literal.</summary>
    Number,

    /// <summary>Identifies an operator or capability modifier.</summary>
    Operator,

    /// <summary>Identifies signature punctuation.</summary>
    Punctuation,
}
