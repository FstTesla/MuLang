using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectPropertyInitializerSyntax(
    SyntaxToken NameToken,
    SyntaxToken SeparatorToken,
    ExpressionSyntax Value
) : SyntaxNode
{
    public bool IsOptional => SeparatorToken.Kind == TokenKind.OptionalPropertyColon;

    public override TextSpan Span => TextSpan.FromBounds(NameToken.Span.Start, Value.Span.End);
}
