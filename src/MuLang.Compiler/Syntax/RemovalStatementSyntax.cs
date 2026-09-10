using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record RemovalStatementSyntax(
    ExpressionSyntax Target,
    SyntaxToken TildeToken,
    SyntaxToken? SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        Target.Span.Start,
        SemicolonToken?.Span.End ?? TildeToken.Span.End
    );
}
