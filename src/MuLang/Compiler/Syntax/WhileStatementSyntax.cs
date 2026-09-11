using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record WhileStatementSyntax(
    SyntaxToken WhileKeyword,
    SyntaxToken OpenParenthesisToken,
    ExpressionSyntax Condition,
    SyntaxToken CloseParenthesisToken,
    StatementSyntax Body
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(WhileKeyword.Span.Start, Body.Span.End);
}
