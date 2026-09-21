namespace MuLang.IR;

/// <summary>Identifies the role of an IR slot.</summary>
public enum IrSlotKind
{
    /// <summary>A function parameter.</summary>
    Parameter,

    /// <summary>A source-level local variable.</summary>
    Local,

    /// <summary>A compiler-generated temporary value.</summary>
    Temporary,
}
