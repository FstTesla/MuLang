using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record CallExpressionSyntax(
    ExpressionSyntax Target,
    SyntaxToken OpenParenthesisToken,
    IReadOnlyList<ExpressionSyntax> Arguments,
    IReadOnlyList<SyntaxToken> CommaTokens,
    SyntaxToken CloseParenthesisToken
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        Target.Span.Start,
        CloseParenthesisToken.Span.End
    );
}
