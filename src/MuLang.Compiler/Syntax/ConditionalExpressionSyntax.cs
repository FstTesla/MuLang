using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ConditionalExpressionSyntax(
    ExpressionSyntax Condition,
    SyntaxToken QuestionToken,
    ExpressionSyntax WhenTrue,
    SyntaxToken ColonToken,
    ExpressionSyntax WhenFalse
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(Condition.Span.Start, WhenFalse.Span.End);
}
