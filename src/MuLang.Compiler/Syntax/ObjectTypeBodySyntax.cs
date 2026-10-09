using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectTypeBodySyntax(
    SyntaxToken OpenBraceToken,
    IReadOnlyList<ObjectTypePropertySyntax> Properties,
    IReadOnlyList<SyntaxToken> CommaTokens,
    SyntaxToken CloseBraceToken
) : TypePrimarySyntax
{
    public bool IsOpen => OpenBraceToken.Kind == TokenKind.OpenObjectBrace;

    public override TextSpan Span => TextSpan.FromBounds(
        OpenBraceToken.Span.Start,
        CloseBraceToken.Span.End
    );
}
