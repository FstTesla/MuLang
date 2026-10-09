using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectTypePropertySyntax(
    SyntaxToken NameToken,
    SyntaxToken? DollarToken,
    SyntaxToken? QuestionToken,
    SyntaxToken ColonToken,
    TypeSyntax Type
) : SyntaxNode
{
    public bool IsOptional => QuestionToken is not null;

    public bool IsReadOnly => DollarToken is not null;

    public override TextSpan Span => TextSpan.FromBounds(
        NameToken.Span.Start,
        Type.Span.End
    );
}
