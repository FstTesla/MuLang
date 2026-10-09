using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TypeSyntax(
    TypePrimarySyntax Primary,
    IReadOnlyList<SyntaxToken> SuffixTokens
) : SyntaxNode
{
    public override TextSpan Span => SuffixTokens is not [ .., var lastSuffixToken ]
        ? Primary.Span
        : TextSpan.FromBounds(Primary.Span.Start, lastSuffixToken.Span.End);
}
