using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record TryStatementSyntax(
    SyntaxToken TryKeyword,
    BlockStatementSyntax Body,
    CatchClauseSyntax? CatchClause,
    FinallyClauseSyntax? FinallyClause
) : StatementSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        TryKeyword.Span.Start,
        FinallyClause?.Span.End ?? CatchClause?.Span.End ?? Body.Span.End
    );
}
