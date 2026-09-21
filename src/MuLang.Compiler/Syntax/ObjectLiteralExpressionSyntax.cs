using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectLiteralExpressionSyntax(
    SyntaxToken OpenBraceToken,
    IReadOnlyList<ObjectPropertyInitializerSyntax> Properties,
    IReadOnlyList<SyntaxToken> CommaTokens,
    SyntaxToken CloseBraceToken
) : ExpressionSyntax
{
    public bool IsOpen => OpenBraceToken.Kind == TokenKind.OpenObjectBrace;

    public override TextSpan Span => TextSpan.FromBounds(
        OpenBraceToken.Span.Start,
        CloseBraceToken.Span.End
    );
}
