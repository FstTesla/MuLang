using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ObjectPropertyInitializerSyntax(
    SyntaxToken NameToken,
    SyntaxToken ColonToken,
    ExpressionSyntax Value
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(NameToken.Span.Start, Value.Span.End);
}
