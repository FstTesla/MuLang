using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record AssignmentStatementSyntax(
    ExpressionSyntax Target,
    SyntaxToken EqualToken,
    ExpressionSyntax Value,
    SyntaxToken? SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        Target.Span.Start,
        SemicolonToken?.Span.End ?? Value.Span.End
    );
}
