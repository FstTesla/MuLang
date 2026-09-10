using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ProgramRootSyntax(
    IReadOnlyList<StatementSyntax> Statements,
    SyntaxToken EndOfFileToken
) : RootSyntax
{
    public override TextSpan Span => Statements.Count == 0
        ? EndOfFileToken.Span
        : TextSpan.FromBounds(Statements[0].Span.Start, EndOfFileToken.Span.End);
}
