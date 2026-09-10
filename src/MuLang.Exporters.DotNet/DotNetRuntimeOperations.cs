using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Exporters.DotNet;

internal static class DotNetRuntimeOperations
{
    public static void ValidateEnvironment(
        DotNetRuntimeContext context,
        EnvironmentFingerprint expected,
        TextSpan span
    )
    {
        if (context.EnvironmentFingerprint != expected)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.EnvironmentMismatch,
                "The runtime environment is incompatible with the compiled program.",
                span
            );
        }
    }

    public static void Consume(DotNetRuntimeContext context, TextSpan span)
    {
        context.Consume(span);
    }

    public static object? GetGlobal(
        DotNetRuntimeContext context,
        string id,
        TypeSymbol expectedType,
        TextSpan span
    )
    {
        return context.GetGlobal(id, expectedType, span);
    }

    public static object? Invoke(
        DotNetRuntimeContext context,
        string id,
        object?[] arguments,
        TypeSymbol returnType,
        TextSpan span
    )
    {
        return context.Invoke(id, Array.AsReadOnly(arguments), returnType, span);
    }

    public static bool RequireBoolean(object? value, TextSpan span)
    {
        return value is bool boolean
            ? boolean
            : throw InvalidValue("Expected a Boolean value.", span);
    }

    public static object? Unary(
        IrUnaryOperator operation,
        object? operand,
        TextSpan span
    )
    {
        return operation switch
        {
            IrUnaryOperator.Identity => operand,
            IrUnaryOperator.Negate => Negate(operand, span),
            IrUnaryOperator.LogicalNot => !RequireBoolean(operand, span),
            IrUnaryOperator.BitwiseNot => ~RequireInt(operand, span),
            _ => throw new InvalidOperationException("Unknown IR unary operator."),
        };
    }

    public static bool IsNull(object? value)
    {
        return value is null;
    }

    public static object? Binary(
        DotNetRuntimeContext context,
        IrBinaryOperator operation,
        object? left,
        object? right,
        TextSpan span
    )
    {
        return operation switch
        {
            IrBinaryOperator.Add => Add(left, right, span),
            IrBinaryOperator.Subtract => Subtract(left, right, span),
            IrBinaryOperator.Multiply => Multiply(left, right, span),
            IrBinaryOperator.Divide => Divide(left, right, span),
            IrBinaryOperator.Remainder => Remainder(left, right, span),
            IrBinaryOperator.LeftShift => LeftShift(left, right, span),
            IrBinaryOperator.RightShift => RightShift(left, right, span),
            IrBinaryOperator.LessThan =>
                CompareRelational(left, right, span, static comparison => comparison < 0),
            IrBinaryOperator.LessThanOrEqual =>
                CompareRelational(left, right, span, static comparison => comparison <= 0),
            IrBinaryOperator.GreaterThan =>
                CompareRelational(left, right, span, static comparison => comparison > 0),
            IrBinaryOperator.GreaterThanOrEqual =>
                CompareRelational(left, right, span, static comparison => comparison >= 0),
            IrBinaryOperator.StructuralEqual =>
                StructuralEquals(context, left, right, span),
            IrBinaryOperator.StructuralNotEqual =>
                !StructuralEquals(context, left, right, span),
            IrBinaryOperator.IdentityEqual =>
                IdentityEquals(context, left, right, span),
            IrBinaryOperator.IdentityNotEqual =>
                !IdentityEquals(context, left, right, span),
            IrBinaryOperator.BitwiseAnd =>
                RequireInt(left, span) & RequireInt(right, span),
            IrBinaryOperator.BitwiseXor =>
                RequireInt(left, span) ^ RequireInt(right, span),
            IrBinaryOperator.BitwiseOr =>
                RequireInt(left, span) | RequireInt(right, span),
            _ => throw new InvalidOperationException("Unknown IR binary operator."),
        };
    }

    public static object? ConvertValue(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol targetType,
        TextSpan span
    )
    {
        if (targetType is NullableTypeSymbol nullable)
        {
            return value is null
                ? null
                : ConvertValue(context, value, nullable.UnderlyingType, span);
        }

        if (targetType.Kind == TypeKind.String)
        {
            return ConvertToString(value, span);
        }

        if (value is null)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.NullValue,
                $"Null cannot be converted to '{targetType.DisplayName}'.",
                span
            );
        }

        return targetType.Kind switch
        {
            TypeKind.Bool when value is bool => value,
            TypeKind.Int => ConvertToInt(value, span),
            TypeKind.Number => ConvertToNumber(value, span),
            TypeKind.Unknown => value,
            TypeKind.Object when IsObject(value) => value,
            TypeKind.StructuredObject
                when IsValueOfTypeDeep(context, value, targetType, span, 0) => value,
            TypeKind.Array
                when IsValueOfTypeDeep(context, value, targetType, span, 0) => value,
            _ => throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidConversion,
                $"Runtime value cannot be converted to '{targetType.DisplayName}'.",
                span
            ),
        };
    }

    public static bool TypeTest(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol type,
        TextSpan span
    )
    {
        return IsValueOfTypeDeep(context, value, type, span, 0);
    }

    internal static bool IsValueOfTypeShallow(object? value, TypeSymbol type)
    {
        if (type is NullableTypeSymbol nullable)
        {
            return value is null || IsValueOfTypeShallow(value, nullable.UnderlyingType);
        }

        if (value is null)
        {
            return false;
        }

        return type.Kind switch
        {
            TypeKind.Bool => value is bool,
            TypeKind.Int => value is long,
            TypeKind.Number => value is double,
            TypeKind.String => value is string,
            TypeKind.Unknown => true,
            TypeKind.Object => IsObject(value),
            TypeKind.StructuredObject => IsObject(value),
            TypeKind.Array => TryGetArrayCount(value, out _),
            _ => false,
        };
    }

    public static object CreateArray(object?[] elements)
    {
        return new DotNetArrayValue(elements);
    }

    public static object CreateObject(string[] names, object?[] values)
    {
        KeyValuePair<string, object?>[] properties =
            new KeyValuePair<string, object?>[names.Length];

        for (int index = 0; index < names.Length; index++)
        {
            properties[index] = new KeyValuePair<string, object?>(
                names[index],
                values[index]
            );
        }

        return new DotNetObjectValue(properties);
    }

    public static object? GetProperty(
        object? target,
        string name,
        bool isOptional,
        bool isArrayLength,
        TypeSymbol expectedType,
        TextSpan span
    )
    {
        if (target is null)
        {
            if (isOptional)
            {
                return null;
            }

            throw NullTarget(span);
        }

        if (isArrayLength)
        {
            return EnsureValueType(
                (long)GetArrayCount(target, span),
                expectedType,
                span
            );
        }

        if (TryGetProperty(target, name, out object? value))
        {
            return EnsureValueType(value, expectedType, span);
        }

        if (isOptional)
        {
            return null;
        }

        throw new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.MissingProperty,
            $"Property '{name}' does not exist.",
            span
        );
    }

    public static void SetProperty(
        object? target,
        string name,
        object? value,
        TextSpan span
    )
    {
        if (target is null)
        {
            throw NullTarget(span);
        }

        bool updated = target switch
        {
            IDotNetObjectValue objectValue =>
                objectValue.TrySetProperty(name, value),
            IDictionary<string, object?> dictionary =>
                SetDictionaryProperty(dictionary, name, value),
            _ => false,
        };

        if (!updated)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.MutationRejected,
                $"Property '{name}' cannot be assigned.",
                span
            );
        }
    }

    public static void RemoveProperty(
        object? target,
        string name,
        TextSpan span
    )
    {
        if (target is null)
        {
            throw NullTarget(span);
        }

        bool removed = target switch
        {
            IDotNetObjectValue objectValue =>
                objectValue.TryRemoveProperty(name),
            IDictionary<string, object?> dictionary =>
                dictionary.Remove(name),
            _ => false,
        };

        if (!removed)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.RemovalRejected,
                $"Property '{name}' cannot be removed or does not exist.",
                span
            );
        }
    }

    public static object? GetElement(
        object? target,
        object? index,
        bool isObjectAccess,
        bool isOptional,
        TypeSymbol expectedType,
        TextSpan span
    )
    {
        if (target is null)
        {
            if (isOptional)
            {
                return null;
            }

            throw NullTarget(span);
        }

        if (isObjectAccess)
        {
            string key = RequireString(index, span);
            return GetProperty(
                target,
                key,
                isOptional,
                false,
                expectedType,
                span
            );
        }

        int arrayIndex = RequireIndex(index, span);

        if (TryGetArrayElement(target, arrayIndex, out object? value))
        {
            return EnsureValueType(value, expectedType, span);
        }

        throw new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.InvalidIndex,
            $"Array index {arrayIndex} is outside the valid range.",
            span
        );
    }

    public static void SetElement(
        object? target,
        object? index,
        object? value,
        bool isObjectAccess,
        TextSpan span
    )
    {
        if (target is null)
        {
            throw NullTarget(span);
        }

        if (isObjectAccess)
        {
            SetProperty(target, RequireString(index, span), value, span);
            return;
        }

        int arrayIndex = RequireIndex(index, span);
        bool updated = target switch
        {
            IDotNetArrayValue arrayValue =>
                arrayValue.TrySetElement(arrayIndex, value),
            IList<object?> list
                when arrayIndex >= 0 && arrayIndex < list.Count =>
                    SetListElement(list, arrayIndex, value),
            Array array
                when arrayIndex >= 0 && arrayIndex < array.Length =>
                    SetArrayElement(array, arrayIndex, value),
            _ => false,
        };

        if (!updated)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.MutationRejected,
                $"Array element {arrayIndex} cannot be assigned.",
                span
            );
        }
    }

    public static void RemoveElementProperty(
        object? target,
        object? key,
        TextSpan span
    )
    {
        RemoveProperty(target, RequireString(key, span), span);
    }

    public static bool HasProperty(object? target, object? key, TextSpan span)
    {
        if (target is null)
        {
            throw NullTarget(span);
        }

        return TryGetProperty(target, RequireString(key, span), out _);
    }

    private static object Negate(object? value, TextSpan span)
    {
        if (value is long integer)
        {
            try
            {
                return checked(-integer);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(span, exception);
            }
        }

        return -RequireNumber(value, span);
    }

    private static object Add(object? left, object? right, TextSpan span)
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
                throw IntegerOverflow(span, exception);
            }
        }

        return RequireNumber(left, span) + RequireNumber(right, span);
    }

    private static object Subtract(object? left, object? right, TextSpan span)
    {
        if (left is long leftInt && right is long rightInt)
        {
            try
            {
                return checked(leftInt - rightInt);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(span, exception);
            }
        }

        return RequireNumber(left, span) - RequireNumber(right, span);
    }

    private static object Multiply(object? left, object? right, TextSpan span)
    {
        if (left is long leftInt && right is long rightInt)
        {
            try
            {
                return checked(leftInt * rightInt);
            }
            catch (OverflowException exception)
            {
                throw IntegerOverflow(span, exception);
            }
        }

        return RequireNumber(left, span) * RequireNumber(right, span);
    }

    private static object Divide(object? left, object? right, TextSpan span)
    {
        if (left is long leftInt && right is long rightInt)
        {
            if (rightInt == 0)
            {
                throw DivisionByZero(span);
            }

            if (leftInt == long.MinValue && rightInt == -1)
            {
                throw IntegerOverflow(span);
            }

            return leftInt / rightInt;
        }

        return RequireNumber(left, span) / RequireNumber(right, span);
    }

    private static object Remainder(object? left, object? right, TextSpan span)
    {
        if (left is long leftInt && right is long rightInt)
        {
            if (rightInt == 0)
            {
                throw DivisionByZero(span);
            }

            if (leftInt == long.MinValue && rightInt == -1)
            {
                throw IntegerOverflow(span);
            }

            return leftInt % rightInt;
        }

        return RequireNumber(left, span) % RequireNumber(right, span);
    }

    private static long LeftShift(object? left, object? right, TextSpan span)
    {
        long value = RequireInt(left, span);
        int shift = RequireShift(right, span);

        return value << shift;
    }

    private static long RightShift(object? left, object? right, TextSpan span)
    {
        long value = RequireInt(left, span);
        int shift = RequireShift(right, span);

        return value >> shift;
    }

    private static bool CompareRelational(
        object? left,
        object? right,
        TextSpan span,
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

        if (left is string leftString && right is string rightString)
        {
            return evaluateComparison(
                StringComparer.Ordinal.Compare(leftString, rightString)
            );
        }

        throw InvalidValue("Values cannot be ordered.", span);
    }

    private static bool StructuralEquals(
        DotNetRuntimeContext context,
        object? left,
        object? right,
        TextSpan span
    )
    {
        HashSet<ReferencePair> visited = new(ReferencePairComparer.Instance);
        return StructuralEquals(context, left, right, visited, span, 0);
    }

    private static bool StructuralEquals(
        DotNetRuntimeContext context,
        object? left,
        object? right,
        ISet<ReferencePair> visited,
        TextSpan span,
        int depth
    )
    {
        EnsureTraversalDepth(depth, span);
        context.Consume(span);

        if (left is double leftDouble && right is double rightDouble)
        {
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
            return IntegerEqualsNumber(leftInt, rightNumber);
        }

        if (left is double leftNumber && right is long rightInt)
        {
            return IntegerEqualsNumber(rightInt, leftNumber);
        }

        if (
            left is bool or long or double or string ||
            right is bool or long or double or string
        )
        {
            return Equals(left, right);
        }

        object leftIdentity = GetIdentity(left);
        object rightIdentity = GetIdentity(right);
        ReferencePair pair = new(leftIdentity, rightIdentity);

        if (!visited.Add(pair))
        {
            return true;
        }

        if (TryGetArrayCount(left, out int leftCount))
        {
            if (!TryGetArrayCount(right, out int rightCount) || leftCount != rightCount)
            {
                return false;
            }

            for (int index = 0; index < leftCount; index++)
            {
                TryGetArrayElement(left, index, out object? leftValue);
                TryGetArrayElement(right, index, out object? rightValue);

                if (!StructuralEquals(
                    context,
                    leftValue,
                    rightValue,
                    visited,
                    span,
                    depth + 1
                ))
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetPropertyNames(left, out IReadOnlyCollection<string>? leftNames))
        {
            return false;
        }

        if (
            !TryGetPropertyNames(right, out IReadOnlyCollection<string>? rightNames) ||
            leftNames.Count != rightNames.Count
        )
        {
            return false;
        }

        foreach (string name in leftNames)
        {
            if (
                !TryGetProperty(left, name, out object? leftValue) ||
                !TryGetProperty(right, name, out object? rightValue) ||
                !StructuralEquals(
                    context,
                    leftValue,
                    rightValue,
                    visited,
                    span,
                    depth + 1
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IdentityEquals(
        DotNetRuntimeContext context,
        object? left,
        object? right,
        TextSpan span
    )
    {
        if (
            left is null ||
            right is null ||
            left is bool or long or double or string ||
            right is bool or long or double or string
        )
        {
            return StructuralEquals(context, left, right, span);
        }

        return ReferenceEquals(GetIdentity(left), GetIdentity(right));
    }

    private static bool IsValueOfTypeDeep(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol type,
        TextSpan span,
        int depth
    )
    {
        EnsureTraversalDepth(depth, span);
        context.Consume(span);

        if (type is NullableTypeSymbol nullable)
        {
            return value is null ||
                IsValueOfTypeDeep(
                    context,
                    value,
                    nullable.UnderlyingType,
                    span,
                    depth + 1
                );
        }

        if (value is null)
        {
            return false;
        }

        return type.Kind switch
        {
            TypeKind.Bool => value is bool,
            TypeKind.Int => value is long,
            TypeKind.Number => value is double,
            TypeKind.String => value is string,
            TypeKind.Unknown => true,
            TypeKind.Object => IsObject(value),
            TypeKind.StructuredObject => IsStructuredObject(
                context,
                value,
                (ObjectTypeSymbol)type,
                span,
                depth + 1
            ),
            TypeKind.Array => IsArrayOfType(
                context,
                value,
                (ArrayTypeSymbol)type,
                span,
                depth + 1
            ),
            _ => false,
        };
    }

    private static bool IsStructuredObject(
        DotNetRuntimeContext context,
        object value,
        ObjectTypeSymbol type,
        TextSpan span,
        int depth
    )
    {
        if (!TryGetPropertyNames(value, out IReadOnlyCollection<string>? names))
        {
            return false;
        }

        foreach (ObjectPropertySymbol property in type.Properties)
        {
            if (!TryGetProperty(value, property.Name, out object? propertyValue))
            {
                if (property.IsOptional)
                {
                    continue;
                }

                return false;
            }

            if (!IsValueOfTypeDeep(
                context,
                propertyValue,
                property.Type,
                span,
                depth + 1
            ))
            {
                return false;
            }
        }

        if (!type.IsOpen)
        {
            HashSet<string> declaredNames = type.Properties
                .Select(static property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            return names.All(declaredNames.Contains);
        }

        HashSet<string> knownNames = type.Properties
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string name in names)
        {
            if (
                !knownNames.Contains(name) &&
                (
                    !TryGetProperty(value, name, out object? additionalValue) ||
                    additionalValue is null
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsArrayOfType(
        DotNetRuntimeContext context,
        object value,
        ArrayTypeSymbol type,
        TextSpan span,
        int depth
    )
    {
        if (!TryGetArrayCount(value, out int count))
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            if (
                !TryGetArrayElement(value, index, out object? element) ||
                !IsValueOfTypeDeep(
                    context,
                    element,
                    type.ElementType,
                    span,
                    depth + 1
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static string ConvertToString(object? value, TextSpan span)
    {
        return value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            long integer => integer.ToString(CultureInfo.InvariantCulture),
            double number when double.IsNaN(number) => "NaN",
            double number when double.IsPositiveInfinity(number) => "Infinity",
            double number when double.IsNegativeInfinity(number) => "-Infinity",
            double number => FormatNumber(number),
            string text => text,
            _ => throw InvalidValue("Value has no intrinsic string conversion.", span),
        };
    }

    private static long ConvertToInt(object value, TextSpan span)
    {
        if (value is long integer)
        {
            return integer;
        }

        if (
            value is double number &&
            double.IsFinite(number) &&
            Math.Truncate(number) == number &&
            number >= long.MinValue &&
            number <= long.MaxValue
        )
        {
            try
            {
                return checked((long)number);
            }
            catch (OverflowException exception)
            {
                throw new MuLangRuntimeException(
                    DotNetRuntimeErrorCodes.InvalidConversion,
                    "Value cannot be converted to int.",
                    span,
                    exception
                );
            }
        }

        throw InvalidValue("Value cannot be converted to int.", span);
    }

    private static double ConvertToNumber(object value, TextSpan span)
    {
        return value switch
        {
            long integer => integer,
            double number => number,
            _ => throw InvalidValue("Value cannot be converted to number.", span),
        };
    }

    private static long RequireInt(object? value, TextSpan span)
    {
        return value is long integer
            ? integer
            : throw InvalidValue("Expected an int value.", span);
    }

    private static double RequireNumber(object? value, TextSpan span)
    {
        return value is double number
            ? number
            : throw InvalidValue("Expected a number value.", span);
    }

    private static string RequireString(object? value, TextSpan span)
    {
        return value is string text
            ? text
            : throw InvalidValue("Expected a string value.", span);
    }

    private static int RequireIndex(object? value, TextSpan span)
    {
        long index = RequireInt(value, span);

        if (index < int.MinValue || index > int.MaxValue)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidIndex,
                $"Array index {index} is outside the supported range.",
                span
            );
        }

        return (int)index;
    }

    private static int RequireShift(object? value, TextSpan span)
    {
        long shift = RequireInt(value, span);

        if (shift < 0 || shift > 63)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidShift,
                $"Shift count {shift} must be between 0 and 63.",
                span
            );
        }

        return (int)shift;
    }

    private static bool IsObject(object value)
    {
        return value is
            IDotNetObjectValue or
            IDictionary<string, object?> or
            IReadOnlyDictionary<string, object?>;
    }

    private static object GetIdentity(object value)
    {
        return value switch
        {
            IDotNetObjectValue objectValue => objectValue.Identity,
            IDotNetArrayValue arrayValue => arrayValue.Identity,
            _ => value,
        };
    }

    private static bool TryGetProperty(
        object target,
        string name,
        out object? value
    )
    {
        switch (target)
        {
            case IDotNetObjectValue objectValue:
            {
                return objectValue.TryGetProperty(name, out value);
            }
            case IDictionary<string, object?> dictionary:
            {
                return dictionary.TryGetValue(name, out value);
            }
            case IReadOnlyDictionary<string, object?> dictionary:
            {
                return dictionary.TryGetValue(name, out value);
            }
            default:
            {
                value = null;
                return false;
            }
        }
    }

    private static bool TryGetPropertyNames(
        object target,
        [NotNullWhen(true)] out IReadOnlyCollection<string>? names
    )
    {
        switch (target)
        {
            case IDotNetObjectValue objectValue:
            {
                names = objectValue.PropertyNames;
                return true;
            }
            case IDictionary<string, object?> dictionary:
            {
                names = dictionary.Keys.ToArray();
                return true;
            }
            case IReadOnlyDictionary<string, object?> dictionary:
            {
                names = dictionary.Keys.ToArray();
                return true;
            }
            default:
            {
                names = null;
                return false;
            }
        }
    }

    private static int GetArrayCount(object target, TextSpan span)
    {
        if (TryGetArrayCount(target, out int count))
        {
            return count;
        }

        throw InvalidValue("Expected an array value.", span);
    }

    private static bool TryGetArrayCount(object target, out int count)
    {
        switch (target)
        {
            case IDotNetArrayValue arrayValue:
            {
                count = arrayValue.Count;
                return true;
            }
            case IList<object?> list:
            {
                count = list.Count;
                return true;
            }
            case IReadOnlyList<object?> list:
            {
                count = list.Count;
                return true;
            }
            case Array array:
            {
                if (array.Rank != 1)
                {
                    count = 0;
                    return false;
                }

                count = array.Length;
                return true;
            }
            default:
            {
                count = 0;
                return false;
            }
        }
    }

    private static bool TryGetArrayElement(
        object target,
        int index,
        out object? value
    )
    {
        switch (target)
        {
            case IDotNetArrayValue arrayValue:
            {
                return arrayValue.TryGetElement(index, out value);
            }
            case IList<object?> list when index >= 0 && index < list.Count:
            {
                value = list[index];
                return true;
            }
            case IReadOnlyList<object?> list when index >= 0 && index < list.Count:
            {
                value = list[index];
                return true;
            }
            case Array array
                when array.Rank == 1 && index >= 0 && index < array.Length:
            {
                value = array.GetValue(index + array.GetLowerBound(0));
                return true;
            }
            default:
            {
                value = null;
                return false;
            }
        }
    }

    private static bool SetDictionaryProperty(
        IDictionary<string, object?> dictionary,
        string name,
        object? value
    )
    {
        try
        {
            dictionary[name] = value;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool SetListElement(
        IList<object?> list,
        int index,
        object? value
    )
    {
        try
        {
            list[index] = value;
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool SetArrayElement(Array array, int index, object? value)
    {
        try
        {
            array.SetValue(value, index + array.GetLowerBound(0));
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidCastException or ArgumentException
        )
        {
            return false;
        }
    }

    private static MuLangRuntimeException NullTarget(TextSpan span)
    {
        return new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.NullValue,
            "Operation target is null.",
            span
        );
    }

    private static MuLangRuntimeException InvalidValue(string message, TextSpan span)
    {
        return new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.InvalidRuntimeValue,
            message,
            span
        );
    }

    private static MuLangRuntimeException IntegerOverflow(
        TextSpan span,
        Exception? innerException = null
    )
    {
        return new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.IntegerOverflow,
            "Integer arithmetic overflowed.",
            span,
            innerException
        );
    }

    private static MuLangRuntimeException DivisionByZero(TextSpan span)
    {
        return new MuLangRuntimeException(
            DotNetRuntimeErrorCodes.DivisionByZero,
            "Integer division or remainder by zero is not permitted.",
            span
        );
    }

    private static object? EnsureValueType(
        object? value,
        TypeSymbol expectedType,
        TextSpan span
    )
    {
        if (!IsValueOfTypeShallow(value, expectedType))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Runtime value is incompatible with '{expectedType.DisplayName}'.",
                span
            );
        }

        return value;
    }

    private static bool IntegerEqualsNumber(long integer, double number)
    {
        if (
            !double.IsFinite(number) ||
            Math.Truncate(number) != number ||
            number < long.MinValue ||
            number >= 9223372036854775808.0
        )
        {
            return false;
        }

        return integer == (long)number;
    }

    private static string FormatNumber(double number)
    {
        string text = number.ToString("R", CultureInfo.InvariantCulture);

        return text.Contains('.') ||
            text.Contains('e', StringComparison.OrdinalIgnoreCase)
                ? text
                : $"{text}.0";
    }

    private static void EnsureTraversalDepth(int depth, TextSpan span)
    {
        if (depth > 256)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                "Runtime value nesting exceeds the supported depth.",
                span
            );
        }
    }
}
