using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ArrayLiteralExpressionSyntax(
    SyntaxToken OpenBracketToken,
    IReadOnlyCollection<ExpressionSyntax> Elements,
    IReadOnlyCollection<SyntaxToken> CommaTokens,
    SyntaxToken CloseBracketToken
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        OpenBracketToken.Span.Start,
        CloseBracketToken.Span.End
    );
}
