using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ConversionExpressionSyntax(
    ExpressionSyntax Expression,
    SyntaxToken AsKeyword,
    TypeSyntax Type
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(Expression.Span.Start, Type.Span.End);
}
