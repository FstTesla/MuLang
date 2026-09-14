using MuLang.Compiler.Syntax;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal abstract record BoundExpression(
    SyntaxNode Syntax,
    TypeSymbol Type
) : BoundNode(Syntax)
{
    internal sealed record Error(SyntaxNode Syntax)
        : BoundExpression(Syntax, TypeSymbols.Error);

    internal sealed record Literal(
        SyntaxNode Syntax,
        TypeSymbol Type,
        object? Value
    ) : BoundExpression(Syntax, Type);

    internal sealed record Local(
        SyntaxNode Syntax,
        LocalSymbol Symbol
    ) : BoundExpression(Syntax, Symbol.Type);

    internal sealed record Parameter(
        SyntaxNode Syntax,
        UserParameterSymbol Symbol
    ) : BoundExpression(Syntax, Symbol.Type);

    internal sealed record Global(
        SyntaxNode Syntax,
        GlobalSymbol Symbol
    ) : BoundExpression(Syntax, Symbol.Type);

    internal sealed record Array(
        SyntaxNode Syntax,
        ArrayTypeSymbol ArrayType,
        IReadOnlyList<BoundExpression> Elements
    ) : BoundExpression(Syntax, ArrayType);

    internal sealed record ObjectProperty(
        string Name,
        BoundExpression Value
    );

    internal sealed record Object(
        SyntaxNode Syntax,
        ObjectTypeSymbol ObjectType,
        IReadOnlyList<ObjectProperty> Properties
    ) : BoundExpression(Syntax, ObjectType);

    internal sealed record Unary(
        SyntaxNode Syntax,
        TypeSymbol Type,
        TokenKind Operator,
        BoundExpression Operand
    ) : BoundExpression(Syntax, Type);

    internal sealed record Binary(
        SyntaxNode Syntax,
        TypeSymbol Type,
        BoundExpression Left,
        TokenKind Operator,
        BoundExpression Right
    ) : BoundExpression(Syntax, Type);

    internal sealed record Conversion(
        SyntaxNode Syntax,
        TypeSymbol Type,
        BoundExpression Expression,
        ConversionKind ConversionKind
    ) : BoundExpression(Syntax, Type);

    internal sealed record Truthiness(
        SyntaxNode Syntax,
        BoundExpression Expression
    ) : BoundExpression(Syntax, TypeSymbols.Bool);

    internal sealed record TypeTest(
        SyntaxNode Syntax,
        BoundExpression Expression,
        TypeSymbol TestedType
    ) : BoundExpression(Syntax, TypeSymbols.Bool);

    internal sealed record PropertyTest(
        SyntaxNode Syntax,
        BoundExpression Target,
        BoundExpression Key
    ) : BoundExpression(Syntax, TypeSymbols.Bool);

    internal sealed record Conditional(
        SyntaxNode Syntax,
        TypeSymbol Type,
        BoundExpression Condition,
        BoundExpression WhenTrue,
        BoundExpression WhenFalse
    ) : BoundExpression(Syntax, Type);

    internal sealed record ProviderCall(
        SyntaxNode Syntax,
        FunctionSymbol Function,
        IReadOnlyList<BoundExpression> Arguments
    ) : BoundExpression(Syntax, Function.ReturnType);

    internal sealed record UserCall(
        SyntaxNode Syntax,
        UserFunctionSymbol Function,
        IReadOnlyList<BoundExpression> Arguments
    ) : BoundExpression(Syntax, Function.ReturnType);

    internal sealed record MemberAccess(
        SyntaxNode Syntax,
        TypeSymbol Type,
        BoundExpression Target,
        string Name,
        ObjectPropertySymbol? Property,
        bool IsDynamic,
        bool IsOptional,
        bool IsArrayLength
    ) : BoundExpression(Syntax, Type);

    internal sealed record ElementAccess(
        SyntaxNode Syntax,
        TypeSymbol Type,
        BoundExpression Target,
        BoundExpression Index,
        ObjectPropertySymbol? Property,
        bool IsObjectAccess,
        bool IsDynamic,
        bool IsOptional
    ) : BoundExpression(Syntax, Type);
}
