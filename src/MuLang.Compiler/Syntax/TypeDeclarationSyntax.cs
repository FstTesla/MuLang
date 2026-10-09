using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TypeDeclarationSyntax(
    SyntaxToken TypeKeyword,
    SyntaxToken IdentifierToken,
    ObjectTypeBodySyntax Body,
    SyntaxToken SemicolonToken
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(
        TypeKeyword.Span.Start,
        SemicolonToken.Span.End
    );
}
