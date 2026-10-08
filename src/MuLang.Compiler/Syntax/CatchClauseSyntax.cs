using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record CatchClauseSyntax(
    SyntaxToken CatchKeyword,
    SyntaxToken? OpenParenthesisToken,
    SyntaxToken? IdentifierToken,
    SyntaxToken? CloseParenthesisToken,
    BlockStatementSyntax Body
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(
        CatchKeyword.Span.Start,
        Body.Span.End
    );
}
