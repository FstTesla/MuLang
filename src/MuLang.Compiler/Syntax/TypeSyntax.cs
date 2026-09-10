using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TypeSyntax(
    SyntaxToken NameToken,
    IReadOnlyList<SyntaxToken> SuffixTokens
) : SyntaxNode
{
    public override TextSpan Span => SuffixTokens.Count == 0
        ? NameToken.Span
        : TextSpan.FromBounds(NameToken.Span.Start, SuffixTokens[^1].Span.End);
}
