using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ForStatementSyntax(
    SyntaxToken ForKeyword,
    SyntaxToken OpenParenthesisToken,
    StatementSyntax? Initializer,
    SyntaxToken FirstSemicolonToken,
    ExpressionSyntax? Condition,
    SyntaxToken SecondSemicolonToken,
    StatementSyntax? Iterator,
    SyntaxToken CloseParenthesisToken,
    StatementSyntax Body
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(ForKeyword.Span.Start, Body.Span.End);
}
