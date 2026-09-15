namespace MuLang.Core.Types;

/// <summary>Specifies the kind of conversion between two MuLang types.</summary>
public enum ConversionKind
{
    /// <summary>Indicates that no conversion exists.</summary>
    None,
    /// <summary>Indicates that source and target types are equivalent.</summary>
    Identity,
    /// <summary>Indicates an implicit conversion.</summary>
    Implicit,
    /// <summary>Indicates a conversion that requires a runtime check.</summary>
    Checked,
}
