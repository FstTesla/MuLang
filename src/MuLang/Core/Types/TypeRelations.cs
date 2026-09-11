namespace MuLang.Core.Types;

public static class TypeRelations
{
    public static bool AreEquivalent(TypeSymbol left, TypeSymbol right)
    {
        ValidateTypes(left, right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Kind != right.Kind)
        {
            return false;
        }

        return (left, right) switch
        {
            (NullableTypeSymbol leftNullable, NullableTypeSymbol rightNullable) =>
                AreEquivalent(leftNullable.UnderlyingType, rightNullable.UnderlyingType),
            (ArrayTypeSymbol leftArray, ArrayTypeSymbol rightArray) =>
                AreEquivalent(leftArray.ElementType, rightArray.ElementType),
            (ObjectTypeSymbol leftObject, ObjectTypeSymbol rightObject) =>
                AreEquivalent(leftObject, rightObject),
            _ => false,
        };
    }

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

        if (target.Kind == TypeKind.Void || source.Kind == TypeKind.Void)
        {
            return false;
        }

        if (IsAssignable(source, target))
        {
            return true;
        }

        if (source.Kind == TypeKind.Unknown)
        {
            return true;
        }

        if (source is NullableTypeSymbol sourceNullable)
        {
            return CanConvertChecked(sourceNullable.UnderlyingType, target);
        }

        if (
            source.Kind == TypeKind.Number &&
            target.Kind is TypeKind.Int or TypeKind.Float
        )
        {
            return true;
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

        if (
            source.Kind is TypeKind.Object or TypeKind.StructuredObject &&
            target.Kind is TypeKind.Object or TypeKind.StructuredObject
        )
        {
            return true;
        }

        return target is NullableTypeSymbol targetNullable &&
            CanConvertChecked(source, targetNullable.UnderlyingType);
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

    private static bool AreEquivalent(
        ObjectTypeSymbol left,
        ObjectTypeSymbol right
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
                !AreEquivalent(leftProperty.Type, rightProperty.Type)
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
}
