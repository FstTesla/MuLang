namespace MuLang.Compiler;

/// <summary>Specifies an editor-oriented lexical source classification.</summary>
public enum SourceClassificationKind
{
    /// <summary>Identifies invalid source text.</summary>
    Invalid,

    /// <summary>Identifies a language keyword.</summary>
    Keyword,

    /// <summary>Identifies an identifier.</summary>
    Identifier,

    /// <summary>Identifies a numeric literal.</summary>
    Number,

    /// <summary>Identifies a string literal.</summary>
    String,

    /// <summary>Identifies an operator.</summary>
    Operator,

    /// <summary>Identifies punctuation or a delimiter.</summary>
    Punctuation,
}
