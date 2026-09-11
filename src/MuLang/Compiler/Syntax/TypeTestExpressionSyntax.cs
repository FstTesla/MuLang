using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TypeTestExpressionSyntax(
    ExpressionSyntax Expression,
    SyntaxToken IsKeyword,
    TypeSyntax Type
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(Expression.Span.Start, Type.Span.End);
}
