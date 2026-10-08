using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record FinallyClauseSyntax(
    SyntaxToken FinallyKeyword,
    BlockStatementSyntax Body
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(
        FinallyKeyword.Span.Start,
        Body.Span.End
    );
}
