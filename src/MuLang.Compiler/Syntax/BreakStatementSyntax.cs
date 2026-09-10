using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record BreakStatementSyntax(
    SyntaxToken BreakKeyword,
    SyntaxToken SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        BreakKeyword.Span.Start,
        SemicolonToken.Span.End
    );
}
