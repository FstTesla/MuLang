using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ExpressionRootSyntax(
    ExpressionSyntax Expression,
    SyntaxToken EndOfFileToken
) : RootSyntax
{
    public override TextSpan Span => TextSpan.FromBounds(
        Expression.Span.Start,
        EndOfFileToken.Span.End
    );
}
