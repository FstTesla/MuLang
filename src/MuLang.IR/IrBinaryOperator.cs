namespace MuLang.IR;

/// <summary>Identifies a binary IR operation.</summary>
public enum IrBinaryOperator
{
    /// <summary>Addition.</summary>
    Add,

    /// <summary>Subtraction.</summary>
    Subtract,

    /// <summary>Multiplication.</summary>
    Multiply,

    /// <summary>Division.</summary>
    Divide,

    /// <summary>Remainder.</summary>
    Remainder,

    /// <summary>Left shift.</summary>
    LeftShift,

    /// <summary>Right shift.</summary>
    RightShift,

    /// <summary>Less-than comparison.</summary>
    LessThan,

    /// <summary>Less-than-or-equal comparison.</summary>
    LessThanOrEqual,

    /// <summary>Greater-than comparison.</summary>
    GreaterThan,

    /// <summary>Greater-than-or-equal comparison.</summary>
    GreaterThanOrEqual,

    /// <summary>Structural equality comparison.</summary>
    StructuralEqual,

    /// <summary>Structural inequality comparison.</summary>
    StructuralNotEqual,

    /// <summary>Identity equality comparison.</summary>
    IdentityEqual,

    /// <summary>Identity inequality comparison.</summary>
    IdentityNotEqual,

    /// <summary>Bitwise AND.</summary>
    BitwiseAnd,

    /// <summary>Bitwise exclusive OR.</summary>
    BitwiseXor,

    /// <summary>Bitwise OR.</summary>
    BitwiseOr,
}
