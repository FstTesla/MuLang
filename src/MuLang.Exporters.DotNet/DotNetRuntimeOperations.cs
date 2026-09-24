using MuLang.Core.Environment;
using MuLang.Core.Evaluation;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using System.Diagnostics.CodeAnalysis;

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
        IReadOnlyList<object?> arguments,
        TypeSymbol returnType,
        TextSpan span
    )
    {
        return context.Invoke(id, arguments, returnType, span);
    }

    public static bool RequireBoolean(object? value, TextSpan span)
    {
        return value is bool boolean
            ? boolean
            : throw InvalidValue("Expected a Boolean value.", span);
    }

    public static bool Truthiness(object? value, TextSpan span)
    {
        if (PrimitiveValueOperations.TryGetTruthiness(value, out bool result))
        {
            return result;
        }

        return value switch
        {
            IDotNetObjectValue => true,
            IDotNetReadOnlyArrayValue => true,
            IDotNetArrayValue => true,
            _ => throw InvalidValue(
                "Runtime value has an unsupported truthiness representation.",
                span
            ),
        };
    }

    public static object? Unary(
        IrUnaryOperator operation,
        object? operand,
        TextSpan span
    )
    {
        return EvaluatePrimitive(
            () => PrimitiveValueOperations.EvaluateUnary(
                MapUnaryOperation(operation),
                operand
            ),
            span
        );
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
            IrBinaryOperator.StructuralEqual =>
                StructuralEquals(context, left, right, span),
            IrBinaryOperator.StructuralNotEqual =>
                !StructuralEquals(context, left, right, span),
            IrBinaryOperator.IdentityEqual =>
                IdentityEquals(context, left, right, span),
            IrBinaryOperator.IdentityNotEqual =>
                !IdentityEquals(context, left, right, span),
            _ => EvaluatePrimitive(
                () => PrimitiveValueOperations.EvaluateBinary(
                    MapBinaryOperation(operation),
                    left,
                    right
                ),
                span
            ),
        };
    }

    public static object? ConvertValue(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol targetType,
        IrConversionKind conversionKind,
        TextSpan span
    )
    {
        if (conversionKind == IrConversionKind.CheckedCast)
        {
            if (IsValueOfTypeDeep(context, value, targetType, span))
            {
                return value;
            }

            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidConversion,
                $"Runtime value cannot be cast to '{targetType.DisplayName}'.",
                span
            );
        }

        if (PrimitiveValueOperations.IsPrimitiveType(targetType))
        {
            return EvaluatePrimitive(
                () => PrimitiveValueOperations.ConvertValue(value, targetType),
                span
            );
        }

        if (targetType is NullableTypeSymbol nullable)
        {
            return value is null
                ? null
                : ConvertValue(
                    context,
                    value,
                    nullable.UnderlyingType,
                    IrConversionKind.ValueConversion,
                    span
                );
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
            TypeKind.Object when IsObject(value) => value,
            TypeKind.StructuredObject
                when IsValueOfTypeDeep(context, value, targetType, span) => value,
            TypeKind.Array
                when IsValueOfTypeDeep(context, value, targetType, span) => value,
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
        return IsValueOfTypeDeep(context, value, type, span);
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
            TypeKind.Float => value is double,
            TypeKind.Number => value is long or double,
            TypeKind.String => value is string,
            TypeKind.Unknown => true,
            TypeKind.Object => IsObject(value),
            TypeKind.StructuredObject => IsObject(value),
            TypeKind.Array => type is ArrayTypeSymbol arrayType &&
            (
                arrayType.IsReadOnly
                    ? value is IDotNetReadOnlyArrayValue or IDotNetArrayValue
                    : value is IDotNetArrayValue
            ),
            _ => false,
        };
    }

    internal static bool IsValueOfTypeDeep(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol type,
        TextSpan span
    )
    {
        IDictionary<object, ISet<TypeSymbol>> activeTypes =
            new Dictionary<object, ISet<TypeSymbol>>(ReferenceEqualityComparer.Instance);
        return IsValueOfTypeDeep(context, value, type, activeTypes, span, 0);
    }

    public static object CreateArray(
        IEnumerable<object?> elements,
        bool isReadOnly
    )
    {
        return isReadOnly
            ? new DotNetReadOnlyArrayValue(elements)
            : new DotNetArrayValue(elements);
    }

    public static object CreateObject(IEnumerable<string> names, IEnumerable<object?> values)
    {
        IEnumerable<KeyValuePair<string, object?>> properties =
            names.Zip(values).Select(static nv => KeyValuePair.Create(nv.First, nv.Second));

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
            return isOptional ? null : throw NullTarget(span);
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

        if (!isOptional)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.MissingProperty,
                $"Property '{name}' does not exist.",
                span
            );
        }

        return null;
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

        bool updated = target is IDotNetObjectValue objectValue && objectValue.TrySetProperty(name, value);

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

        bool removed = target is IDotNetObjectValue objectValue && objectValue.TryRemoveProperty(name);

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
        DotNetRuntimeContext context,
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
            return isOptional ? null : throw NullTarget(span);
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

        if (!TryGetArrayElement(target, arrayIndex, out object? value))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidIndex,
                $"Array index {arrayIndex} is outside the valid range.",
                span
            );
        }

        return EnsureValueTypeDeep(context, value, expectedType, span);
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
        bool updated = target is IDotNetArrayValue arrayValue && arrayValue.TrySetElement(arrayIndex, value);

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
        return target is null
            ? throw NullTarget(span)
            : TryGetProperty(target, RequireString(key, span), out _);
    }

    private static bool StructuralEquals(
        DotNetRuntimeContext context,
        object? left,
        object? right,
        TextSpan span
    )
    {
        ISet<ReferencePair> visited = new HashSet<ReferencePair>(ReferencePairComparer.Instance);
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
        EnsureTraversalDepth(context, depth, span);
        context.Consume(span);

        if (
            PrimitiveValueOperations.IsPrimitiveValue(left) ||
            PrimitiveValueOperations.IsPrimitiveValue(right)
        )
        {
            return (bool)PrimitiveValueOperations.EvaluateBinary(
                PrimitiveBinaryOperation.StructuralEqual,
                left,
                right
            );
        }

        object leftIdentity = GetIdentity(left!);
        object rightIdentity = GetIdentity(right!);
        ReferencePair pair = new (leftIdentity, rightIdentity);

        if (!visited.Add(pair))
        {
            return true;
        }

        if (TryGetArrayCount(left!, out int leftCount))
        {
            if (!TryGetArrayCount(right!, out int rightCount) || leftCount != rightCount)
            {
                return false;
            }

            for (int index = 0; index < leftCount; index++)
            {
                TryGetArrayElement(left!, index, out object? leftValue);
                TryGetArrayElement(right!, index, out object? rightValue);

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

        if (!TryGetPropertyNames(left!, out IReadOnlyCollection<string>? leftNames))
        {
            return false;
        }

        if (
            !TryGetPropertyNames(right!, out IReadOnlyCollection<string>? rightNames) ||
            leftNames.Count != rightNames.Count
        )
        {
            return false;
        }

        foreach (string name in leftNames)
        {
            if (
                !TryGetProperty(left!, name, out object? leftValue) ||
                !TryGetProperty(right!, name, out object? rightValue) ||
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
            left is long && right is double ||
            left is double && right is long
        )
        {
            return false;
        }

        if (
            PrimitiveValueOperations.IsPrimitiveValue(left) ||
            PrimitiveValueOperations.IsPrimitiveValue(right)
        )
        {
            EnsureTraversalDepth(context, 0, span);
            context.Consume(span);
            return (bool)PrimitiveValueOperations.EvaluateBinary(
                PrimitiveBinaryOperation.IdentityEqual,
                left,
                right
            );
        }

        return ReferenceEquals(GetIdentity(left!), GetIdentity(right!));
    }

    private static bool IsValueOfTypeDeep(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol type,
        IDictionary<object, ISet<TypeSymbol>> activeTypes,
        TextSpan span,
        int depth
    )
    {
        object? identity =
            RequiresIdentityTracking(type) &&
            value is
                IDotNetObjectValue or
                IDotNetReadOnlyArrayValue or
                IDotNetArrayValue
                ? GetIdentity(value)
                : null;
        ISet<TypeSymbol>? types = null;

        if (identity is not null)
        {
            if (activeTypes.TryGetValue(identity, out types))
            {
                if (
                    types.Any(
                        activeType =>
                            TypeRelations.IsAssignable(activeType, type) &&
                            TypeRelations.IsCastable(activeType, type)
                    )
                )
                {
                    return true;
                }
            }
            else
            {
                types = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
                activeTypes.Add(identity, types);
            }

            types.Add(type);
        }

        try
        {
            EnsureTraversalDepth(context, depth, span);
            context.Consume(span);

            if (type is NullableTypeSymbol nullable)
            {
                return value is null ||
                    IsValueOfTypeDeep(
                        context,
                        value,
                        nullable.UnderlyingType,
                        activeTypes,
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
                TypeKind.Float => value is double,
                TypeKind.Number => value is long or double,
                TypeKind.String => value is string,
                TypeKind.Unknown => true,
                TypeKind.Object => IsGenericObject(
                    context,
                    value,
                    activeTypes,
                    span,
                    depth + 1
                ),
                TypeKind.StructuredObject => IsStructuredObject(
                    context,
                    value,
                    (ObjectTypeSymbol)type,
                    activeTypes,
                    span,
                    depth + 1
                ),
                TypeKind.Array => IsArrayOfType(
                    context,
                    value,
                    (ArrayTypeSymbol)type,
                    activeTypes,
                    span,
                    depth + 1
                ),
                _ => false,
            };
        }
        finally
        {
            if (identity is not null && types is not null)
            {
                types.Remove(type);

                if (types.Count == 0)
                {
                    activeTypes.Remove(identity);
                }
            }
        }
    }

    private static bool RequiresIdentityTracking(TypeSymbol type)
    {
        return type is NullableTypeSymbol nullable
            ? RequiresIdentityTracking(nullable.UnderlyingType)
            : type.Kind is
                TypeKind.Object or
                TypeKind.StructuredObject or
                TypeKind.Array;
    }

    private static bool IsStructuredObject(
        DotNetRuntimeContext context,
        object value,
        ObjectTypeSymbol type,
        IDictionary<object, ISet<TypeSymbol>> activeTypes,
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
                    activeTypes,
                    span,
                    depth + 1
                ))
            {
                return false;
            }
        }

        if (!type.IsOpen)
        {
            IReadOnlySet<string> declaredNames = type.Properties
                .Select(static property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            return names.All(declaredNames.Contains);
        }

        IReadOnlySet<string> knownNames = type.Properties
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string name in names)
        {
            if (
                !knownNames.Contains(name) &&
                (
                    !TryGetProperty(value, name, out object? additionalValue) ||
                    !IsValueOfTypeDeep(
                        context,
                        additionalValue,
                        TypeSymbols.Nullable(TypeSymbols.Unknown),
                        activeTypes,
                        span,
                        depth + 1
                    )
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsGenericObject(
        DotNetRuntimeContext context,
        object value,
        IDictionary<object, ISet<TypeSymbol>> activeTypes,
        TextSpan span,
        int depth
    )
    {
        if (!TryGetPropertyNames(value, out IReadOnlyCollection<string>? names))
        {
            return false;
        }

        foreach (string name in names)
        {
            if (
                !TryGetProperty(value, name, out object? propertyValue) ||
                !IsValueOfTypeDeep(
                    context,
                    propertyValue,
                    TypeSymbols.Nullable(TypeSymbols.Unknown),
                    activeTypes,
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

    private static bool IsArrayOfType(
        DotNetRuntimeContext context,
        object value,
        ArrayTypeSymbol type,
        IDictionary<object, ISet<TypeSymbol>> activeTypes,
        TextSpan span,
        int depth
    )
    {
        if (
            type.IsReadOnly
                ? value is not IDotNetReadOnlyArrayValue and not IDotNetArrayValue
                : value is not IDotNetArrayValue
        )
        {
            return false;
        }

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
                    activeTypes,
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

    private static long RequireInt(object? value, TextSpan span)
    {
        return value is long integer
            ? integer
            : throw InvalidValue("Expected an int value.", span);
    }

    private static string RequireString(object? value, TextSpan span)
    {
        return value as string ?? throw InvalidValue("Expected a string value.", span);
    }

    private static int RequireIndex(object? value, TextSpan span)
    {
        long index = RequireInt(value, span);

        if (index is < int.MinValue or > int.MaxValue)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidIndex,
                $"Array index {index} is outside the supported range.",
                span
            );
        }

        return (int)index;
    }

    private static T EvaluatePrimitive<T>(
        Func<T> evaluate,
        TextSpan span
    )
    {
        try
        {
            return evaluate();
        }
        catch (PrimitiveOperationException exception)
        {
            string code = exception.Error switch
            {
                PrimitiveOperationError.InvalidValue =>
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                PrimitiveOperationError.IntegerOverflow =>
                    DotNetRuntimeErrorCodes.IntegerOverflow,
                PrimitiveOperationError.DivisionByZero =>
                    DotNetRuntimeErrorCodes.DivisionByZero,
                PrimitiveOperationError.InvalidShift =>
                    DotNetRuntimeErrorCodes.InvalidShift,
                _ => throw new InvalidOperationException(
                    "Unknown primitive operation error."
                ),
            };

            throw new MuLangRuntimeException(
                code,
                exception.Message,
                span,
                exception.InnerException
            );
        }
    }

    private static PrimitiveUnaryOperation MapUnaryOperation(
        IrUnaryOperator operation
    )
    {
        return operation switch
        {
            IrUnaryOperator.Identity => PrimitiveUnaryOperation.Identity,
            IrUnaryOperator.Negate => PrimitiveUnaryOperation.Negate,
            IrUnaryOperator.LogicalNot => PrimitiveUnaryOperation.LogicalNot,
            IrUnaryOperator.BitwiseNot => PrimitiveUnaryOperation.BitwiseNot,
            _ => throw new InvalidOperationException("Unknown IR unary operator."),
        };
    }

    private static PrimitiveBinaryOperation MapBinaryOperation(
        IrBinaryOperator operation
    )
    {
        return operation switch
        {
            IrBinaryOperator.Add => PrimitiveBinaryOperation.Add,
            IrBinaryOperator.Subtract => PrimitiveBinaryOperation.Subtract,
            IrBinaryOperator.Multiply => PrimitiveBinaryOperation.Multiply,
            IrBinaryOperator.Divide => PrimitiveBinaryOperation.Divide,
            IrBinaryOperator.Remainder => PrimitiveBinaryOperation.Remainder,
            IrBinaryOperator.LeftShift => PrimitiveBinaryOperation.LeftShift,
            IrBinaryOperator.RightShift => PrimitiveBinaryOperation.RightShift,
            IrBinaryOperator.LessThan => PrimitiveBinaryOperation.LessThan,
            IrBinaryOperator.LessThanOrEqual =>
                PrimitiveBinaryOperation.LessThanOrEqual,
            IrBinaryOperator.GreaterThan => PrimitiveBinaryOperation.GreaterThan,
            IrBinaryOperator.GreaterThanOrEqual =>
                PrimitiveBinaryOperation.GreaterThanOrEqual,
            IrBinaryOperator.BitwiseAnd => PrimitiveBinaryOperation.BitwiseAnd,
            IrBinaryOperator.BitwiseXor => PrimitiveBinaryOperation.BitwiseXor,
            IrBinaryOperator.BitwiseOr => PrimitiveBinaryOperation.BitwiseOr,
            _ => throw new InvalidOperationException(
                "IR binary operator is not a primitive scalar operation."
            ),
        };
    }

    private static bool IsObject(object value)
    {
        return value is IDotNetObjectValue;
    }

    private static object GetIdentity(object value)
    {
        return value switch
        {
            IDotNetObjectValue objectValue => objectValue.Identity,
            IDotNetReadOnlyArrayValue arrayValue => arrayValue.Identity,
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
        if (target is IDotNetObjectValue objectValue)
        {
            return objectValue.TryGetProperty(name, out value);
        }
        else
        {
            value = null;
            return false;
        }
    }

    private static bool TryGetPropertyNames(
        object target,
        [NotNullWhen(true)] out IReadOnlyCollection<string>? names
    )
    {
        if (target is IDotNetObjectValue objectValue)
        {
            names = objectValue.PropertyNames;
            return true;
        }
        else
        {
            names = null;
            return false;
        }
    }

    private static int GetArrayCount(object target, TextSpan span)
    {
        return TryGetArrayCount(target, out int count)
            ? count
            : throw InvalidValue("Expected an array value.", span);
    }

    private static bool TryGetArrayCount(object target, out int count)
    {
        if (target is IDotNetReadOnlyArrayValue arrayValue)
        {
            count = arrayValue.Count;
            return true;
        }
        else if (target is IDotNetArrayValue mutableArrayValue)
        {
            count = mutableArrayValue.Count;
            return true;
        }
        else
        {
            count = 0;
            return false;
        }
    }

    private static bool TryGetArrayElement(
        object target,
        int index,
        out object? value
    )
    {
        if (target is IDotNetReadOnlyArrayValue arrayValue)
        {
            return arrayValue.TryGetElement(index, out value);
        }
        else if (target is IDotNetArrayValue mutableArrayValue)
        {
            return mutableArrayValue.TryGetElement(index, out value);
        }
        else
        {
            value = null;
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

    private static object? EnsureValueTypeDeep(
        DotNetRuntimeContext context,
        object? value,
        TypeSymbol expectedType,
        TextSpan span
    )
    {
        if (!IsValueOfTypeDeep(context, value, expectedType, span))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Runtime value is incompatible with '{expectedType.DisplayName}'.",
                span
            );
        }

        return value;
    }

    private static void EnsureTraversalDepth(
        DotNetRuntimeContext context,
        int depth,
        TextSpan span
    )
    {
        if (depth > context.MaximumTraversalDepth)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                "Runtime value nesting exceeds the supported depth.",
                span
            );
        }
    }
}
