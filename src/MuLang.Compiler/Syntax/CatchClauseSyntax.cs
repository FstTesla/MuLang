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
    public TextSpan? ParameterSpan =>
        OpenParenthesisToken is not null &&
        IdentifierToken is not null &&
        CloseParenthesisToken is not null
            ? TextSpan.FromBounds(
                OpenParenthesisToken.Span.Start,
                CloseParenthesisToken.Span.End
            )
            : null;

    public override TextSpan Span => TextSpan.FromBounds(
        CatchKeyword.Span.Start,
        Body.Span.End
    );
}
