using MuLang.Compiler.Syntax;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal abstract record BoundRoot(SyntaxNode Syntax) : BoundNode(Syntax)
{
    internal sealed record Expression(
        SyntaxNode Syntax,
        BoundExpression Value
    ) : BoundRoot(Syntax);

    internal sealed record Program(
        SyntaxNode Syntax,
        IReadOnlyList<BoundFunction> Functions,
        IReadOnlyList<BoundStatement> Statements,
        TypeSymbol ResultType
    ) : BoundRoot(Syntax);
}
