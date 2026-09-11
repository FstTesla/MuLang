using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ParenthesizedExpressionSyntax(
    SyntaxToken OpenParenthesisToken,
    ExpressionSyntax Expression,
    SyntaxToken CloseParenthesisToken
) : ExpressionSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        OpenParenthesisToken.Span.Start,
        CloseParenthesisToken.Span.End
    );
}
