using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ThrowStatementSyntax(
    SyntaxToken ThrowKeyword,
    ExpressionSyntax? Expression,
    SyntaxToken SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        ThrowKeyword.Span.Start,
        SemicolonToken.Span.End
    );
}
