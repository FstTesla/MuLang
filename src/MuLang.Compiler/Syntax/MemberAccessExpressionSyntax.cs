using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record MemberAccessExpressionSyntax(
    ExpressionSyntax Target,
    SyntaxToken OperatorToken,
    SyntaxToken NameToken
) : ExpressionSyntax
{
    public bool IsOptional => OperatorToken.Kind == TokenKind.OptionalDot;

    public override TextSpan Span => TextSpan.FromBounds(Target.Span.Start, NameToken.Span.End);
}
