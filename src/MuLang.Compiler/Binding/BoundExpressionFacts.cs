using MuLang.Compiler.Syntax;
using MuLang.Core.Evaluation;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal static class BoundExpressionFacts
{
    public static bool TryGetTruthiness(
        BoundExpression expression,
        out bool value
    )
    {
        if (expression is BoundExpression.Truthiness truthiness)
        {
            return TryGetTruthiness(truthiness.Expression, out value);
        }

        if (
            expression.Type is not NullableTypeSymbol &&
            (
                expression.Type is ArrayTypeSymbol ||
                expression.Type.Kind is TypeKind.Object or TypeKind.StructuredObject
            )
        )
        {
            value = true;
            return true;
        }

        if (
            !TryEvaluateConstant(expression, out object? constant) ||
            !PrimitiveValueOperations.TryGetTruthiness(constant, out value)
        )
        {
            value = false;
            return false;
        }

        return true;
    }

    public static bool TryGetNullness(
        BoundExpression expression,
        out bool isNull
    )
    {
        if (TryEvaluateConstant(expression, out object? constant))
        {
            isNull = constant is null;
            return true;
        }

        if (expression.Type.Kind == TypeKind.Null)
        {
            isNull = true;
            return true;
        }

        if (
            expression.Type is NullableTypeSymbol ||
            expression.Type.Kind is TypeKind.Error or TypeKind.Void
        )
        {
            isNull = false;
            return false;
        }

        isNull = false;
        return true;
    }

    public static bool TryGetTypeTestResult(
        BoundExpression expression,
        TypeSymbol testedType,
        out bool value
    )
    {
        if (
            TypeRelations.IsConformanceGuaranteed(
                expression.Type,
                testedType
            )
        )
        {
            value = true;
            return true;
        }

        if (
            TryEvaluateConstant(expression, out object? constant) &&
            PrimitiveValueOperations.IsPrimitiveType(testedType)
        )
        {
            value = PrimitiveValueOperations.IsValueOfType(
                constant,
                testedType
            );
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryEvaluateConstant(
        BoundExpression expression,
        out object? value
    )
    {
        try
        {
            return TryEvaluateConstantCore(expression, out value);
        }
        catch (PrimitiveOperationException)
        {
            value = null;
            return false;
        }
    }

    private static bool TryEvaluateConstantCore(
        BoundExpression expression,
        out object? value
    )
    {
        switch (expression)
        {
            case BoundExpression.Literal literal:
            {
                value = literal.Value;
                return true;
            }

            case BoundExpression.Unary unary
                when TryEvaluateConstantCore(unary.Operand, out object? operand):
            {
                value = BoundPrimitiveOperations.EvaluateUnary(
                    unary.Operator,
                    operand
                );
                return true;
            }

            case BoundExpression.Binary binary:
            {
                return TryEvaluateBinary(binary, out value);
            }

            case BoundExpression.Coalescing coalescing
                when TryEvaluateConstantCore(
                    coalescing.Left,
                    out object? left
                ):
            {
                if (left is not null)
                {
                    value = left;
                    return true;
                }

                return TryEvaluateConstantCore(coalescing.Right, out value);
            }

            case BoundExpression.Conversion conversion
                when TryEvaluateConstantCore(
                    conversion.Expression,
                    out object? operand
                ):
            {
                if (conversion.IsCast)
                {
                    if (
                        !PrimitiveValueOperations.IsValueOfType(
                            operand,
                            conversion.Type
                        )
                    )
                    {
                        value = null;
                        return false;
                    }

                    value = operand;
                    return true;
                }

                value = PrimitiveValueOperations.ConvertValue(
                    operand,
                    conversion.Type
                );
                return true;
            }

            case BoundExpression.Truthiness truthiness
                when TryGetTruthiness(truthiness.Expression, out bool truthy):
            {
                value = truthy;
                return true;
            }

            case BoundExpression.TypeTest typeTest
                when TryEvaluateConstantCore(
                    typeTest.Expression,
                    out object? operand
                ) &&
                PrimitiveValueOperations.IsPrimitiveType(typeTest.TestedType):
            {
                value = PrimitiveValueOperations.IsValueOfType(
                    operand,
                    typeTest.TestedType
                );
                return true;
            }

            case BoundExpression.Conditional conditional
                when TryGetTruthiness(
                    conditional.Condition,
                    out bool condition
                ):
            {
                return TryEvaluateConstantCore(
                    condition
                        ? conditional.WhenTrue
                        : conditional.WhenFalse,
                    out value
                );
            }

            default:
            {
                value = null;
                return false;
            }
        }
    }

    private static bool TryEvaluateBinary(
        BoundExpression.Binary expression,
        out object? value
    )
    {
        if (
            expression.Operator is
                TokenKind.AmpersandAmpersand or TokenKind.PipePipe &&
            TryGetTruthiness(expression.Left, out bool leftTruthiness)
        )
        {
            bool isAnd = expression.Operator == TokenKind.AmpersandAmpersand;

            if (isAnd ? !leftTruthiness : leftTruthiness)
            {
                value = !isAnd;
                return true;
            }

            if (TryGetTruthiness(expression.Right, out bool rightTruthiness))
            {
                value = rightTruthiness;
                return true;
            }

            value = null;
        }

        if (
            !TryEvaluateConstantCore(expression.Left, out object? left) ||
            !TryEvaluateConstantCore(expression.Right, out object? right)
        )
        {
            value = null;
            return false;
        }

        value = BoundPrimitiveOperations.EvaluateBinary(
            expression.Operator,
            left,
            right
        );
        return true;
    }
}
