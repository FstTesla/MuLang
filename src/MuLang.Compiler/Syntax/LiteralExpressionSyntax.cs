using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record LiteralExpressionSyntax(
    SyntaxToken? SignToken,
    SyntaxToken LiteralToken
) : ExpressionSyntax
{
    public override TextSpan Span => SignToken is null
        ? LiteralToken.Span
        : TextSpan.FromBounds(SignToken.Span.Start, LiteralToken.Span.End);
}
