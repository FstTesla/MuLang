using MuLang.Compiler.Syntax;
using MuLang.Core.Evaluation;

namespace MuLang.Compiler.Binding;

internal static class BoundPrimitiveOperations
{
    public static object? EvaluateUnary(TokenKind kind, object? operand)
    {
        PrimitiveUnaryOperation operation = kind switch
        {
            TokenKind.Plus => PrimitiveUnaryOperation.Identity,
            TokenKind.Minus => PrimitiveUnaryOperation.Negate,
            TokenKind.Bang => PrimitiveUnaryOperation.LogicalNot,
            TokenKind.Tilde => PrimitiveUnaryOperation.BitwiseNot,
            _ => throw new InvalidOperationException("Unknown unary operator."),
        };

        return PrimitiveValueOperations.EvaluateUnary(operation, operand);
    }

    public static object EvaluateBinary(
        TokenKind kind,
        object? left,
        object? right
    )
    {
        PrimitiveBinaryOperation operation = kind switch
        {
            TokenKind.Plus => PrimitiveBinaryOperation.Add,
            TokenKind.Minus => PrimitiveBinaryOperation.Subtract,
            TokenKind.Asterisk => PrimitiveBinaryOperation.Multiply,
            TokenKind.Slash => PrimitiveBinaryOperation.Divide,
            TokenKind.Percent => PrimitiveBinaryOperation.Remainder,
            TokenKind.LeftShift => PrimitiveBinaryOperation.LeftShift,
            TokenKind.RightShift => PrimitiveBinaryOperation.RightShift,
            TokenKind.LessThan => PrimitiveBinaryOperation.LessThan,
            TokenKind.LessThanOrEqual => PrimitiveBinaryOperation.LessThanOrEqual,
            TokenKind.GreaterThan => PrimitiveBinaryOperation.GreaterThan,
            TokenKind.GreaterThanOrEqual => PrimitiveBinaryOperation.GreaterThanOrEqual,
            TokenKind.EqualEqual => PrimitiveBinaryOperation.StructuralEqual,
            TokenKind.BangEqual => PrimitiveBinaryOperation.StructuralNotEqual,
            TokenKind.EqualEqualEqual => PrimitiveBinaryOperation.IdentityEqual,
            TokenKind.BangEqualEqual => PrimitiveBinaryOperation.IdentityNotEqual,
            TokenKind.Ampersand => PrimitiveBinaryOperation.BitwiseAnd,
            TokenKind.Caret => PrimitiveBinaryOperation.BitwiseXor,
            TokenKind.Pipe => PrimitiveBinaryOperation.BitwiseOr,
            _ => throw new InvalidOperationException("Unknown binary operator."),
        };

        return PrimitiveValueOperations.EvaluateBinary(operation, left, right);
    }
}
