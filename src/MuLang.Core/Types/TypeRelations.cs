namespace MuLang.Core.Types;

/// <summary>Provides operations for comparing and converting MuLang types.</summary>
public static class TypeRelations
{
    /// <summary>Determines whether two types have equivalent structure and semantics.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns><c>true</c> if the types are equivalent; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either type is <c>null</c>.</exception>
    public static bool AreEquivalent(TypeSymbol left, TypeSymbol right)
    {
        ValidateTypes(left, right);
        return AreEquivalent(left, right, new HashSet<TypePair>());
    }

    private static bool AreEquivalent(
        TypeSymbol left,
        TypeSymbol right,
        ISet<TypePair> active
    )
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Kind != right.Kind)
        {
            return false;
        }

        TypePair pair = new (left, right);

        if (!active.Add(pair))
        {
            return true;
        }

        bool equivalent = (left, right) switch
        {
            (NullableTypeSymbol leftNullable, NullableTypeSymbol rightNullable) =>
                AreEquivalent(
                    leftNullable.UnderlyingType,
                    rightNullable.UnderlyingType,
                    active
                ),
            (ArrayTypeSymbol leftArray, ArrayTypeSymbol rightArray) =>
                leftArray.IsReadOnly == rightArray.IsReadOnly &&
                AreEquivalent(leftArray.ElementType, rightArray.ElementType, active),
            (ObjectTypeSymbol leftObject, ObjectTypeSymbol rightObject) =>
                AreEquivalent(leftObject, rightObject, active),
            _ => false,
        };
        active.Remove(pair);
        return equivalent;
    }

    /// <summary>Determines whether a value of one type can be assigned to another type.</summary>
    /// <param name="source">The source type.</param>
    /// <param name="target">The target type.</param>
    /// <returns><c>true</c> if a value of the source type is assignable to the target type; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either type is <c>null</c>.</exception>
    public static bool IsAssignable(TypeSymbol source, TypeSymbol target)
    {
        ValidateTypes(source, target);

        if (source.Kind == TypeKind.Error || target.Kind == TypeKind.Error)
        {
            return true;
        }

        if (AreEquivalent(source, target))
        {
            return true;
        }

        if (target is NullableTypeSymbol targetNullable)
        {
            if (source.Kind == TypeKind.Null)
            {
                return true;
            }

            TypeSymbol innerSource = source is NullableTypeSymbol sourceNullable
                ? sourceNullable.UnderlyingType : source;
            return IsAssignable(innerSource, targetNullable.UnderlyingType);
        }

        if (source is NullableTypeSymbol)
        {
            return false;
        }

        if (source is ArrayTypeSymbol sourceArray && target is ArrayTypeSymbol targetArray)
        {
            return targetArray.IsReadOnly &&
                IsViewCompatible(sourceArray.ElementType, targetArray.ElementType);
        }

        if (target.Kind == TypeKind.Unknown)
        {
            return source.Kind is not TypeKind.Null and not TypeKind.Void;
        }

        if (
            source.Kind == TypeKind.Int &&
            target.Kind is TypeKind.Float or TypeKind.Number
        )
        {
            return true;
        }

        if (source.Kind == TypeKind.Float && target.Kind == TypeKind.Number)
        {
            return true;
        }

        if (
            target.Kind == TypeKind.Object &&
            source.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return true;
        }

        return source is ObjectTypeSymbol sourceObject &&
            target is ObjectTypeSymbol targetObject &&
            IsObjectAssignable(sourceObject, targetObject);
    }

    /// <summary>Classifies a value conversion from one type to another, independently of checked casts.</summary>
    /// <param name="source">The source type.</param>
    /// <param name="target">The target type.</param>
    /// <returns>The classified conversion kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either type is <c>null</c>.</exception>
    public static ConversionKind ClassifyConversion(TypeSymbol source, TypeSymbol target)
    {
        ValidateTypes(source, target);

        return AreEquivalent(source, target)
            ? ConversionKind.Identity
            : IsAssignable(source, target)
                ? ConversionKind.Implicit
                : CanConvertChecked(source, target)
                    ? ConversionKind.Checked
                    : ConversionKind.None;
    }

    /// <summary>Determines whether a value of one type can be checked for runtime conformance to another type.</summary>
    /// <param name="source">The source type.</param>
    /// <param name="target">The target type.</param>
    /// <returns><c>true</c> if a checked cast from the source type to the target type is permitted; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either type is <c>null</c>.</exception>
    public static bool IsCastable(TypeSymbol source, TypeSymbol target)
    {
        ValidateTypes(source, target);

        if (source.Kind == TypeKind.Error || target.Kind == TypeKind.Error)
        {
            return true;
        }

        if (source.Kind == TypeKind.Void || target.Kind == TypeKind.Void)
        {
            return false;
        }

        if (AreEquivalent(source, target))
        {
            return true;
        }

        if (target is NullableTypeSymbol targetNullable)
        {
            if (source.Kind == TypeKind.Null)
            {
                return true;
            }

            TypeSymbol innerSource = source is NullableTypeSymbol sourceNullable
                ? sourceNullable.UnderlyingType
                : source;
            return IsCastable(innerSource, targetNullable.UnderlyingType);
        }

        if (source is NullableTypeSymbol sourceNullableType)
        {
            return IsCastable(sourceNullableType.UnderlyingType, target);
        }

        if (source.Kind == TypeKind.Null)
        {
            return false;
        }

        if (source.Kind == TypeKind.Unknown)
        {
            return true;
        }

        if (target.Kind == TypeKind.Unknown)
        {
            return true;
        }

        if (
            source.Kind is TypeKind.Int or TypeKind.Float &&
            target.Kind == TypeKind.Number
        )
        {
            return true;
        }

        if (
            source.Kind == TypeKind.Number &&
            target.Kind is TypeKind.Int or TypeKind.Float
        )
        {
            return true;
        }

        if (
            source.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            target.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return true;
        }

        return source is ArrayTypeSymbol && target is ArrayTypeSymbol;
    }

    /// <summary>Gets the most specific type that can represent values of both types.</summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns>The common type, or <c>null</c> when no common type exists.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either type is <c>null</c>.</exception>
    public static TypeSymbol? GetCommonType(TypeSymbol left, TypeSymbol right)
    {
        ValidateTypes(left, right);

        if (left.Kind == TypeKind.Error || right.Kind == TypeKind.Error)
        {
            return TypeSymbols.Error;
        }

        if (left.Kind == TypeKind.Void || right.Kind == TypeKind.Void)
        {
            return null;
        }

        if (AreEquivalent(left, right))
        {
            return left;
        }

        if (left.Kind == TypeKind.Null && CanBeNullable(right))
        {
            return MakeNullable(right);
        }

        if (right.Kind == TypeKind.Null && CanBeNullable(left))
        {
            return MakeNullable(left);
        }

        if (left is NullableTypeSymbol leftNullable)
        {
            TypeSymbol? underlyingCommon = GetCommonType(leftNullable.UnderlyingType, right);

            return underlyingCommon is null
                ? null
                : MakeNullable(underlyingCommon);
        }

        if (right is NullableTypeSymbol rightNullable)
        {
            TypeSymbol? underlyingCommon = GetCommonType(left, rightNullable.UnderlyingType);

            return underlyingCommon is null
                ? null
                : MakeNullable(underlyingCommon);
        }

        if (
            IsNumeric(left) &&
            IsNumeric(right)
        )
        {
            if (left.Kind == TypeKind.Number || right.Kind == TypeKind.Number)
            {
                return TypeSymbols.Number;
            }

            return left.Kind == TypeKind.Float || right.Kind == TypeKind.Float
                ? TypeSymbols.Float
                : TypeSymbols.Int;
        }

        if (left is ArrayTypeSymbol leftArray && right is ArrayTypeSymbol rightArray)
        {
            TypeSymbol? elementType = GetCommonViewType(
                leftArray.ElementType,
                rightArray.ElementType
            );

            if (elementType is not null)
            {
                return TypeSymbols.ReadOnlyArray(elementType);
            }
        }

        if (IsAssignable(left, right))
        {
            return right;
        }

        if (IsAssignable(right, left))
        {
            return left;
        }

        if (
            left.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            right.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return TypeSymbols.Object;
        }

        return null;
    }

    private static bool CanConvertChecked(TypeSymbol source, TypeSymbol target)
    {
        if (source.Kind == TypeKind.Error || target.Kind == TypeKind.Error)
        {
            return true;
        }

        if (source.Kind == TypeKind.Void || target.Kind == TypeKind.Void)
        {
            return false;
        }

        if (IsAssignable(source, target))
        {
            return true;
        }

        if (source is NullableTypeSymbol sourceNullable)
        {
            return CanConvertChecked(sourceNullable.UnderlyingType, target);
        }

        if (
            target.Kind == TypeKind.String &&
            source.Kind is
                TypeKind.Bool or
                TypeKind.Int or
                TypeKind.Float or
                TypeKind.Number or
                TypeKind.String or
                TypeKind.Null
        )
        {
            return true;
        }

        return false;
    }

    internal static bool IsViewCompatible(TypeSymbol source, TypeSymbol target)
    {
        ValidateTypes(source, target);

        if (AreEquivalent(source, target))
        {
            return true;
        }

        if (target is NullableTypeSymbol targetNullable)
        {
            if (source.Kind == TypeKind.Null)
            {
                return true;
            }

            TypeSymbol innerSource = source is NullableTypeSymbol sourceNullable
                ? sourceNullable.UnderlyingType
                : source;
            return IsViewCompatible(innerSource, targetNullable.UnderlyingType);
        }

        if (source is NullableTypeSymbol)
        {
            return false;
        }

        if (target.Kind == TypeKind.Unknown)
        {
            return source.Kind is not TypeKind.Null and not TypeKind.Void;
        }

        if (
            source.Kind is TypeKind.Int or TypeKind.Float &&
            target.Kind == TypeKind.Number
        )
        {
            return true;
        }

        if (
            target.Kind == TypeKind.Object &&
            source.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return true;
        }

        return source is ArrayTypeSymbol sourceArray &&
            target is ArrayTypeSymbol { IsReadOnly: true } targetArray &&
            IsViewCompatible(sourceArray.ElementType, targetArray.ElementType);
    }

    private static bool IsObjectAssignable(
        ObjectTypeSymbol source,
        ObjectTypeSymbol target
    )
    {
        return AreEquivalent(source, target);
    }

    private static TypeSymbol MakeNullable(TypeSymbol type)
    {
        return type is NullableTypeSymbol
            ? type
            : TypeSymbols.Nullable(type);
    }

    private static bool CanBeNullable(TypeSymbol type)
    {
        return type.Kind is not TypeKind.Void and not TypeKind.Null and not TypeKind.Error;
    }

    private static bool IsNumeric(TypeSymbol type)
    {
        return type.Kind is TypeKind.Int or TypeKind.Float or TypeKind.Number;
    }

    private static TypeSymbol? GetCommonViewType(TypeSymbol left, TypeSymbol right)
    {
        if (IsViewCompatible(left, right))
        {
            return right;
        }

        if (IsViewCompatible(right, left))
        {
            return left;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return TypeSymbols.Number;
        }

        if (left is NullableTypeSymbol leftNullable)
        {
            TypeSymbol? underlyingCommon = GetCommonViewType(
                leftNullable.UnderlyingType,
                right
            );

            return underlyingCommon is null
                ? null
                : MakeNullable(underlyingCommon);
        }

        if (right is NullableTypeSymbol rightNullable)
        {
            TypeSymbol? underlyingCommon = GetCommonViewType(
                left,
                rightNullable.UnderlyingType
            );

            return underlyingCommon is null
                ? null
                : MakeNullable(underlyingCommon);
        }

        if (
            left is ArrayTypeSymbol leftArray &&
            right is ArrayTypeSymbol rightArray
        )
        {
            TypeSymbol? elementType = GetCommonViewType(
                leftArray.ElementType,
                rightArray.ElementType
            );

            return elementType is null
                ? null
                : TypeSymbols.ReadOnlyArray(elementType);
        }

        if (
            left.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            right.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return TypeSymbols.Object;
        }

        return null;
    }

    private static bool AreEquivalent(
        ObjectTypeSymbol left,
        ObjectTypeSymbol right,
        ISet<TypePair> active
    )
    {
        if (left.IsOpen != right.IsOpen || left.Properties.Count != right.Properties.Count)
        {
            return false;
        }

        foreach (ObjectPropertySymbol leftProperty in left.Properties)
        {
            if (!right.TryGetProperty(leftProperty.Name, out ObjectPropertySymbol? rightProperty))
            {
                return false;
            }

            if (
                leftProperty.IsOptional != rightProperty.IsOptional ||
                !AreEquivalent(leftProperty.Type, rightProperty.Type, active)
            )
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateTypes(TypeSymbol left, TypeSymbol right)
    {
        if (left is null)
        {
            throw new ArgumentNullException(nameof(left));
        }

        if (right is null)
        {
            throw new ArgumentNullException(nameof(right));
        }
    }

    private readonly record struct TypePair(TypeSymbol Left, TypeSymbol Right);
}
