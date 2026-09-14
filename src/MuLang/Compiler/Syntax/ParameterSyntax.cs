using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ParameterSyntax(
    SyntaxToken IdentifierToken,
    SyntaxToken ColonToken,
    TypeSyntax Type
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(
        IdentifierToken.Span.Start,
        Type.Span.End
    );
}
