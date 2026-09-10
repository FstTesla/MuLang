using MuLang.Core.Text;

namespace MuLang.IR;

internal abstract record IrTerminator(TextSpan Span)
{
    internal sealed record Jump(
        TextSpan Span,
        int TargetBlock
    ) : IrTerminator(Span);

    internal sealed record Branch(
        TextSpan Span,
        int Condition,
        int TrueBlock,
        int FalseBlock
    ) : IrTerminator(Span);

    internal sealed record Return(
        TextSpan Span,
        int? Value
    ) : IrTerminator(Span);
}
