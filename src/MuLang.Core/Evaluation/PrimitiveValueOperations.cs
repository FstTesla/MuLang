using MuLang.Core.Types;
using System.Globalization;

namespace MuLang.Core.Evaluation;

internal static class PrimitiveValueOperations
{
    public static bool IsPrimitiveType(TypeSymbol type)
    {
        if (type is NullableTypeSymbol nullable)
        {
            return IsPrimitiveType(nullable.UnderlyingType);
        }

        return type.Kind is
            TypeKind.Bool or
            TypeKind.Int or
            TypeKind.Float or
            TypeKind.Number or
            TypeKind.String or
            TypeKind.Unknown or
            TypeKind.Null;
    }

    public static bool IsPrimitiveValue(object? value)
    {
        return value is null or bool or long or double or string;
    }

    public static bool TryGetTruthiness(object? value, out bool result)
    {
        switch (value)
        {
            case null:
            {
                result = false;
                return true;
            }

            case bool boolean:
            {
                result = boolean;
                return true;
            }

            case long integer:
            {
                result = integer != 0;
                return true;
            }

            case double number:
            {
                result = number != 0 && !double.IsNaN(number);
                return true;
            }

            case string text:
            {
                result = text.Length != 0;
                return true;
            }

            default:
            {
                result = false;
                return false;
            }
        }
    }

    public static object? EvaluateUnary(
        PrimitiveUnaryOperation operation,
        object? operand
    )
    {
        return operation switch
        {
            PrimitiveUnaryOperation.Identity => operand,
            PrimitiveUnaryOperation.Negate => Negate(operand),
            PrimitiveUnaryOperation.LogicalNot => !RequireBoolean(operand),
            PrimitiveUnaryOperation.BitwiseNot => ~RequireInt(operand),
            _ => throw new InvalidOperationException("Unknown primitive unary operation."),
        };
    }

    public static object EvaluateBinary(
        PrimitiveBinaryOperation operation,
        object? left,
        object? right
    )
    {
        return operation switch
        {
            PrimitiveBinaryOperation.Add => Add(left, right),
            PrimitiveBinaryOperation.Subtract => Subtract(left, right),
            PrimitiveBinaryOperation.Multiply => Multiply(left, right),
            PrimitiveBinaryOperation.Divide => Divide(left, right),
            PrimitiveBinaryOperation.Remainder => Remainder(left, right),
            PrimitiveBinaryOperation.LeftShift => LeftShift(left, right),
            PrimitiveBinaryOperation.RightShift => RightShift(left, right),
            PrimitiveBinaryOperation.LessThan =>
                CompareRelational(left, right, static comparison => comparison < 0),
            PrimitiveBinaryOperation.LessThanOrEqual =>
                CompareRelational(left, right, static comparison => comparison <= 0),
            PrimitiveBinaryOperation.GreaterThan =>
                CompareRelational(left, right, static comparison => comparison > 0),
            PrimitiveBinaryOperation.GreaterThanOrEqual =>
                CompareRelational(left, right, static comparison => comparison >= 0),
            PrimitiveBinaryOperation.StructuralEqual => StructuralEquals(left, right),
            PrimitiveBinaryOperation.StructuralNotEqual => !StructuralEquals(left, right),
            PrimitiveBinaryOperation.IdentityEqual => IdentityEquals(left, right),
            PrimitiveBinaryOperation.IdentityNotEqual => !IdentityEquals(left, right),
            PrimitiveBinaryOperation.BitwiseAnd => And(left, right),
            PrimitiveBinaryOperation.BitwiseXor => Xor(left, right),
            PrimitiveBinaryOperation.BitwiseOr => Or(left, right),
            _ => throw new InvalidOperationException("Unknown primitive binary operation."),
        };
    }

    public static object? ConvertValue(object? value, TypeSymbol targetType)
    {
        if (targetType is NullableTypeSymbol nullable)
        {
            return value is null
                ? null
                : ConvertValue(value, nullable.UnderlyingType);
        }

        if (targetType.Kind == TypeKind.String)
        {
            return ConvertToString(value);
        }

        if (targetType.Kind == TypeKind.Null)
        {
            return value is null
                ? null
                : throw InvalidValue("Value cannot be converted to null.");
        }

        if (value is null)
        {
            throw InvalidValue($"Null cannot be converted to '{targetType.DisplayName}'.");
        }

        return targetType.Kind switch
        {
            TypeKind.Bool when value is bool => value,
            TypeKind.Int when value is long => value,
            TypeKind.Float => ConvertToFloat(value),
            TypeKind.Number when value is long or double => value,
            TypeKind.Unknown => value,
            _ => throw InvalidValue(
                $"Value cannot be converted to '{targetType.DisplayName}'."
            ),
        };
    }

    public static bool IsValueOfType(object? value, TypeSymbol type)
    {
        if (type is NullableTypeSymbol nullable)
        {
            return value is null || IsValueOfType(value, nullable.UnderlyingType);
        }

        if (value is null)
        {
            return type.Kind == TypeKind.Null;
        }

        return type.Kind switch
        {
            TypeKind.Bool => value is bool,
            TypeKind.Int => value is long,
            TypeKind.Float => value is double,
            TypeKind.Number => value is long or double,
            TypeKind.String => value is string,
            TypeKind.Unknown => true,
            _ => false,
        };
    }

    private static object Negate(object? value)
    {
        if (value is long integer)
        {
            try
            {
                return checked(-integer);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(exception);
            }
        }

        return -RequireFloatCompatible(value);
    }

    private static object Add(object? left, object? right)
    {
        if (left is string leftString && right is string rightString)
        {
            return $"{leftString}{rightString}";
        }

        if (left is long leftInt && right is long rightInt)
        {
            try
            {
                return checked(leftInt + rightInt);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(exception);
            }
        }

        return RequireFloatCompatible(left) + RequireFloatCompatible(right);
    }

    private static object Subtract(object? left, object? right)
    {
        if (left is long leftInt && right is long rightInt)
        {
            try
            {
                return checked(leftInt - rightInt);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(exception);
            }
        }

        return RequireFloatCompatible(left) - RequireFloatCompatible(right);
    }

    private static object Multiply(object? left, object? right)
    {
        if (left is long leftInt && right is long rightInt)
        {
            try
            {
                return checked(leftInt * rightInt);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(exception);
            }
        }

        return RequireFloatCompatible(left) * RequireFloatCompatible(right);
    }

    private static object Divide(object? left, object? right)
    {
        if (left is long leftInt && right is long rightInt)
        {
            if (rightInt == 0)
            {
                throw DivisionByZero();
            }

            if (leftInt == long.MinValue && rightInt == -1)
            {
                throw IntegerOverflow();
            }

            return leftInt / rightInt;
        }

        return RequireFloatCompatible(left) / RequireFloatCompatible(right);
    }

    private static object Remainder(object? left, object? right)
    {
        if (left is long leftInt && right is long rightInt)
        {
            if (rightInt == 0)
            {
                throw DivisionByZero();
            }

            if (leftInt == long.MinValue && rightInt == -1)
            {
                throw IntegerOverflow();
            }

            return leftInt % rightInt;
        }

        return RequireFloatCompatible(left) % RequireFloatCompatible(right);
    }

    private static long LeftShift(object? left, object? right)
    {
        return RequireInt(left) << RequireShift(right);
    }

    private static long RightShift(object? left, object? right)
    {
        return RequireInt(left) >> RequireShift(right);
    }

    private static object And(object? left, object? right)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean & rightBoolean;
        }

        return RequireInt(left) & RequireInt(right);
    }

    private static object Xor(object? left, object? right)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean ^ rightBoolean;
        }

        return RequireInt(left) ^ RequireInt(right);
    }

    private static object Or(object? left, object? right)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean | rightBoolean;
        }

        return RequireInt(left) | RequireInt(right);
    }

    private static bool CompareRelational(
        object? left,
        object? right,
        Func<int, bool> evaluateComparison
    )
    {
        if (left is long leftInt && right is long rightInt)
        {
            return evaluateComparison(leftInt.CompareTo(rightInt));
        }

        if (left is double leftNumber && right is double rightNumber)
        {
            return !double.IsNaN(leftNumber) &&
                !double.IsNaN(rightNumber) &&
                evaluateComparison(leftNumber.CompareTo(rightNumber));
        }

        if (left is long or double && right is long or double)
        {
            double promotedLeft = RequireFloatCompatible(left);
            double promotedRight = RequireFloatCompatible(right);

            return !double.IsNaN(promotedLeft) &&
                !double.IsNaN(promotedRight) &&
                evaluateComparison(promotedLeft.CompareTo(promotedRight));
        }

        if (left is string leftString && right is string rightString)
        {
            return evaluateComparison(
                StringComparer.Ordinal.Compare(leftString, rightString)
            );
        }

        throw InvalidValue("Values cannot be ordered.");
    }

    private static bool StructuralEquals(object? left, object? right)
    {
        if (left is double leftDouble && right is double rightDouble)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return leftDouble == rightDouble;
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is long leftInt && right is double rightNumber)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return leftInt == rightNumber;
        }

        if (left is double leftNumber && right is long rightInt)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return leftNumber == rightInt;
        }

        return Equals(left, right);
    }

    private static bool IdentityEquals(object? left, object? right)
    {
        if (
            left is long && right is double ||
            left is double && right is long
        )
        {
            return false;
        }

        return StructuralEquals(left, right);
    }

    private static bool RequireBoolean(object? value)
    {
        return value is bool boolean
            ? boolean
            : throw InvalidValue("Expected a Boolean value.");
    }

    private static long RequireInt(object? value)
    {
        return value is long integer
            ? integer
            : throw InvalidValue("Expected an int value.");
    }

    private static double ConvertToFloat(object value)
    {
        return value switch
        {
            long integer => integer,
            double number => number,
            _ => throw InvalidValue("Value cannot be converted to float."),
        };
    }

    private static double RequireFloatCompatible(object? value)
    {
        return value switch
        {
            long integer => integer,
            double number => number,
            _ => throw InvalidValue("Expected a numeric value."),
        };
    }

    private static int RequireShift(object? value)
    {
        long shift = RequireInt(value);

        if (shift is < 0 or > 63)
        {
            throw new PrimitiveOperationException(
                PrimitiveOperationError.InvalidShift,
                $"Shift count {shift} must be between 0 and 63."
            );
        }

        return (int)shift;
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            long integer => integer.ToString(CultureInfo.InvariantCulture),
            double.NaN => "nan",
            double.PositiveInfinity => "infty",
            double.NegativeInfinity => "-infty",
            double number => FormatNumber(number),
            string text => text,
            _ => throw InvalidValue("Value has no intrinsic string conversion."),
        };
    }

    private static string FormatNumber(double number)
    {
        string text = number.ToString("R", CultureInfo.InvariantCulture);

        return text.Contains('.') ||
            text.Contains('e', StringComparison.OrdinalIgnoreCase)
                ? text
                : $"{text}.0";
    }

    private static PrimitiveOperationException InvalidValue(string message)
    {
        return new PrimitiveOperationException(
            PrimitiveOperationError.InvalidValue,
            message
        );
    }

    private static PrimitiveOperationException IntegerOverflow(
        Exception? innerException = null
    )
    {
        return new PrimitiveOperationException(
            PrimitiveOperationError.IntegerOverflow,
            "Integer arithmetic overflowed.",
            innerException
        );
    }

    private static PrimitiveOperationException DivisionByZero()
    {
        return new PrimitiveOperationException(
            PrimitiveOperationError.DivisionByZero,
            "Integer division or remainder by zero is not permitted."
        );
    }
}
