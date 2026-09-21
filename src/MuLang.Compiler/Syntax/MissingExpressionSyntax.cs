using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record MissingExpressionSyntax(SyntaxToken MissingToken) : ExpressionSyntax
{
    public override TextSpan Span => MissingToken.Span;
}
