using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectPropertyInitializerSyntax(
    SyntaxToken NameToken,
    SyntaxToken? DollarToken,
    SyntaxToken? QuestionToken,
    SyntaxToken? ColonToken,
    TypeSyntax? Type,
    SyntaxToken? EqualToken,
    ExpressionSyntax? Value,
    bool IsLegacy
) : SyntaxNode
{
    public bool IsOptional => QuestionToken is not null;

    public bool IsReadOnly => DollarToken is not null;

    public override TextSpan Span => TextSpan.FromBounds(
        NameToken.Span.Start,
        Value?.Span.End ?? Type?.Span.End ?? NameToken.Span.End
    );
}
