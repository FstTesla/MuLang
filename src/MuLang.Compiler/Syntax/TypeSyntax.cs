using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TypeSyntax(
    SyntaxToken NameToken,
    IReadOnlyList<SyntaxToken> SuffixTokens
) : SyntaxNode
{
    public override TextSpan Span => SuffixTokens is not [ .., var lastSuffixToken ]
        ? NameToken.Span
        : TextSpan.FromBounds(NameToken.Span.Start, lastSuffixToken.Span.End);
}
