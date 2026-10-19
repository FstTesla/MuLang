namespace MuLang.Compiler;

/// <summary>Specifies the category of a semantic hover item.</summary>
public enum SemanticHoverCategory
{
    /// <summary>Identifies a variable.</summary>
    Variable,

    /// <summary>Identifies a parameter.</summary>
    Parameter,

    /// <summary>Identifies a function.</summary>
    Function,

    /// <summary>Identifies a type.</summary>
    Type,

    /// <summary>Identifies an object property.</summary>
    Property,

    /// <summary>Identifies a literal value.</summary>
    Literal,

    /// <summary>Identifies an expression result.</summary>
    Expression,

    /// <summary>Identifies an operator signature.</summary>
    Operator,
}
