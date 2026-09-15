namespace MuLang.Core.Types;

/// <summary>Specifies the kind of a MuLang type.</summary>
public enum TypeKind
{
    /// <summary>Identifies the Boolean type.</summary>
    Bool = 1,
    /// <summary>Identifies the integer type.</summary>
    Int = 2,
    /// <summary>Identifies the general numeric type.</summary>
    Number = 3,
    /// <summary>Identifies the string type.</summary>
    String = 4,
    /// <summary>Identifies the dynamically checked unknown type.</summary>
    Unknown = 5,
    /// <summary>Identifies the general object type.</summary>
    Object = 6,
    /// <summary>Identifies a nullable type.</summary>
    Nullable = 7,
    /// <summary>Identifies an array type.</summary>
    Array = 8,
    /// <summary>Identifies a structured object type.</summary>
    StructuredObject = 9,
    /// <summary>Identifies the absence of a value.</summary>
    Void = 10,
    /// <summary>Identifies the null literal type.</summary>
    Null = 11,
    /// <summary>Identifies a type used to recover from compilation errors.</summary>
    Error = 12,
    /// <summary>Identifies the floating-point type.</summary>
    Float = 13,
}
