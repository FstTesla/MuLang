using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ElementAccessExpressionSyntax(
    ExpressionSyntax Target,
    SyntaxToken OpenBracketToken,
    ExpressionSyntax Index,
    SyntaxToken CloseBracketToken
) : ExpressionSyntax
{
    public bool IsOptional => OpenBracketToken.Kind == TokenKind.OptionalOpenBracket;

    public override TextSpan Span => TextSpan.FromBounds(
        Target.Span.Start,
        CloseBracketToken.Span.End
    );
}
