using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record VariableDeclarationStatementSyntax(
    SyntaxToken VarKeyword,
    SyntaxToken IdentifierToken,
    SyntaxToken? ColonToken,
    TypeSyntax? Type,
    SyntaxToken? EqualToken,
    ExpressionSyntax? Initializer,
    SyntaxToken? SemicolonToken
) : StatementSyntax
{
    public override TextSpan Span
    {
        get
        {
            int end = SemicolonToken?.Span.End
                ?? Initializer?.Span.End
                ?? Type?.Span.End
                ?? IdentifierToken.Span.End;

            return TextSpan.FromBounds(VarKeyword.Span.Start, end);
        }
    }
}
