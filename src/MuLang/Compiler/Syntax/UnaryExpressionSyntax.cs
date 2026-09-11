using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record UnaryExpressionSyntax(
    SyntaxToken OperatorToken,
    ExpressionSyntax Operand
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(OperatorToken.Span.Start, Operand.Span.End);
}
