using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record BinaryExpressionSyntax(
    ExpressionSyntax Left,
    SyntaxToken OperatorToken,
    ExpressionSyntax Right
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(Left.Span.Start, Right.Span.End);
}
