using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record PropertyTestExpressionSyntax(
    ExpressionSyntax Target,
    SyntaxToken HasKeyword,
    ExpressionSyntax Key
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(Target.Span.Start, Key.Span.End);
}
