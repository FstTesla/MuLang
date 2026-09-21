using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record EmptyStatementSyntax(SyntaxToken SemicolonToken) : StatementSyntax
{
    public override TextSpan Span => SemicolonToken.Span;
}
