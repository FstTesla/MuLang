using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ContinueStatementSyntax(
    SyntaxToken ContinueKeyword,
    SyntaxToken? LevelSignToken,
    SyntaxToken? LevelToken,
    SyntaxToken SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        ContinueKeyword.Span.Start,
        SemicolonToken.Span.End
    );
}
