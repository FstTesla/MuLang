namespace MuLang.IR;

/// <summary>Specifies the semantic kind of an IR conversion.</summary>
public enum IrConversionKind
{
    /// <summary>Applies an implicit, contextual, or compiler-required value conversion.</summary>
    ValueConversion,

    /// <summary>Validates runtime conformance and preserves the original value.</summary>
    CheckedCast,
}
