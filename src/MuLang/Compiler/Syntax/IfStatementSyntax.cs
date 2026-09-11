using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record IfStatementSyntax(
    SyntaxToken IfKeyword,
    SyntaxToken OpenParenthesisToken,
    ExpressionSyntax Condition,
    SyntaxToken CloseParenthesisToken,
    StatementSyntax ThenStatement,
    SyntaxToken? ElseKeyword,
    StatementSyntax? ElseStatement
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        IfKeyword.Span.Start,
        ElseStatement?.Span.End ?? ThenStatement.Span.End
    );
}
