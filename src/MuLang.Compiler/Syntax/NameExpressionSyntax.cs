using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record NameExpressionSyntax(SyntaxToken IdentifierToken) : ExpressionSyntax
{
    public override TextSpan Span => IdentifierToken.Span;
}
