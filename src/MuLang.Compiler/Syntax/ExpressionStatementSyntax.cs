using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ExpressionStatementSyntax(
    ExpressionSyntax Expression,
    SyntaxToken? SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        Expression.Span.Start,
        SemicolonToken?.Span.End ?? Expression.Span.End
    );
}
