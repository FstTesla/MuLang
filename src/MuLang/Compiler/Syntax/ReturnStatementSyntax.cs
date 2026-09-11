using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ReturnStatementSyntax(
    SyntaxToken ReturnKeyword,
    ExpressionSyntax? Expression,
    SyntaxToken SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        ReturnKeyword.Span.Start,
        SemicolonToken.Span.End
    );
}
