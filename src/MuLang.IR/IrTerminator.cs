using MuLang.Core.Text;

namespace MuLang.IR;

/// <summary>Represents the control-flow operation that ends an IR basic block.</summary>
/// <param name="Span">The source span associated with the operation.</param>
public abstract record IrTerminator(TextSpan Span)
{
    /// <summary>Transfers control unconditionally to another block.</summary>
    /// <param name="Span">The source span associated with the operation.</param>
    /// <param name="TargetBlock">The target block identifier.</param>
    public sealed record Jump(
        TextSpan Span,
        int TargetBlock
    ) : IrTerminator(Span);

    /// <summary>Transfers control according to a Boolean condition.</summary>
    /// <param name="Span">The source span associated with the operation.</param>
    /// <param name="Condition">The slot containing the condition.</param>
    /// <param name="TrueBlock">The block selected when the condition is true.</param>
    /// <param name="FalseBlock">The block selected when the condition is false.</param>
    public sealed record Branch(
        TextSpan Span,
        int Condition,
        int TrueBlock,
        int FalseBlock
    ) : IrTerminator(Span);

    /// <summary>Returns from the current function.</summary>
    /// <param name="Span">The source span associated with the operation.</param>
    /// <param name="Value">The returned value slot, or <c>null</c> for a void return.</param>
    public sealed record Return(
        TextSpan Span,
        int? Value
    ) : IrTerminator(Span);
}
