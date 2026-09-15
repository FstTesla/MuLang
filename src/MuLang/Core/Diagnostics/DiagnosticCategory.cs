namespace MuLang.Core.Diagnostics;

/// <summary>Specifies the compiler phase that produced a diagnostic.</summary>
public enum DiagnosticCategory
{
    /// <summary>Identifies a lexical diagnostic.</summary>
    Lexical,
    /// <summary>Identifies a syntax diagnostic.</summary>
    Syntax,
    /// <summary>Identifies a name-binding diagnostic.</summary>
    Binding,
    /// <summary>Identifies a type-checking diagnostic.</summary>
    Type,
    /// <summary>Identifies a control-flow diagnostic.</summary>
    ControlFlow,
    /// <summary>Identifies a code-export diagnostic.</summary>
    Exporter,
}
