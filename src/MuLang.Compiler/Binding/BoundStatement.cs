using MuLang.Compiler.Syntax;

namespace MuLang.Compiler.Binding;

internal abstract record BoundStatement(SyntaxNode Syntax) : BoundNode(Syntax)
{
    internal sealed record Block(
        SyntaxNode Syntax,
        IReadOnlyList<BoundStatement> Statements
    ) : BoundStatement(Syntax);

    internal sealed record VariableDeclaration(
        SyntaxNode Syntax,
        LocalSymbol Local,
        BoundExpression? Initializer
    ) : BoundStatement(Syntax);

    internal sealed record Assignment(
        SyntaxNode Syntax,
        BoundExpression Target,
        BoundExpression Value
    ) : BoundStatement(Syntax);

    internal sealed record Removal(
        SyntaxNode Syntax,
        BoundExpression Target
    ) : BoundStatement(Syntax);

    internal sealed record ExpressionStatement(
        SyntaxNode Syntax,
        BoundExpression Value
    ) : BoundStatement(Syntax);

    internal sealed record If(
        SyntaxNode Syntax,
        BoundExpression Condition,
        BoundStatement Then,
        BoundStatement? Else
    ) : BoundStatement(Syntax);

    internal sealed record While(
        SyntaxNode Syntax,
        BoundExpression Condition,
        BoundStatement Body
    ) : BoundStatement(Syntax);

    internal sealed record For(
        SyntaxNode Syntax,
        BoundStatement? Initializer,
        BoundExpression? Condition,
        BoundStatement? Iterator,
        BoundStatement Body
    ) : BoundStatement(Syntax);

    internal sealed record Break(
        SyntaxNode Syntax,
        int Level
    ) : BoundStatement(Syntax);

    internal sealed record Continue(
        SyntaxNode Syntax,
        int Level
    ) : BoundStatement(Syntax);

    internal sealed record Return(
        SyntaxNode Syntax,
        BoundExpression? Value
    ) : BoundStatement(Syntax);

    internal sealed record Empty(SyntaxNode Syntax) : BoundStatement(Syntax);
}
