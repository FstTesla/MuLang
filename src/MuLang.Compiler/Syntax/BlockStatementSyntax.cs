using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record BlockStatementSyntax(
    SyntaxToken OpenBraceToken,
    IReadOnlyList<StatementSyntax> Statements,
    SyntaxToken CloseBraceToken
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        OpenBraceToken.Span.Start,
        CloseBraceToken.Span.End
    );
}
