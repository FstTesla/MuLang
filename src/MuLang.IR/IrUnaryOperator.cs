namespace MuLang.IR;

/// <summary>Identifies a unary IR operation.</summary>
public enum IrUnaryOperator
{
    /// <summary>Returns the operand unchanged.</summary>
    Identity,

    /// <summary>Negates a numeric operand.</summary>
    Negate,

    /// <summary>Applies logical negation.</summary>
    LogicalNot,

    /// <summary>Applies bitwise negation.</summary>
    BitwiseNot,
}
