using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ProgramRootSyntax(
    IReadOnlyList<StatementSyntax> Statements,
    SyntaxToken EndOfFileToken
) : RootSyntax
{
    public override TextSpan Span => Statements is [ var firstStatement, .. ]
        ? TextSpan.FromBounds(firstStatement.Span.Start, EndOfFileToken.Span.End)
        : EndOfFileToken.Span;
}
