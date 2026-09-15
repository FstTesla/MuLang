namespace MuLang.Core.Types;

/// <summary>Provides built-in MuLang types and composite type factories.</summary>
public static class TypeSymbols
{
    /// <summary>Gets the built-in Boolean type.</summary>
    public static TypeSymbol Bool { get; } = new IntrinsicTypeSymbol(TypeKind.Bool, "bool");

    /// <summary>Gets the built-in integer type.</summary>
    public static TypeSymbol Int { get; } = new IntrinsicTypeSymbol(TypeKind.Int, "int");

    /// <summary>Gets the built-in floating-point type.</summary>
    public static TypeSymbol Float { get; } = new IntrinsicTypeSymbol(TypeKind.Float, "float");

    /// <summary>Gets the built-in general numeric type.</summary>
    public static TypeSymbol Number { get; } = new IntrinsicTypeSymbol(TypeKind.Number, "number");

    /// <summary>Gets the built-in string type.</summary>
    public static TypeSymbol String { get; } = new IntrinsicTypeSymbol(TypeKind.String, "string");

    /// <summary>Gets the built-in dynamically checked unknown type.</summary>
    public static TypeSymbol Unknown { get; } = new IntrinsicTypeSymbol(
        TypeKind.Unknown,
        "unknown"
    );

    /// <summary>Gets the built-in general object type.</summary>
    public static TypeSymbol Object { get; } = new IntrinsicTypeSymbol(
        TypeKind.Object,
        "object"
    );

    /// <summary>Gets the built-in void type.</summary>
    public static TypeSymbol Void { get; } = new IntrinsicTypeSymbol(TypeKind.Void, "void");

    internal static TypeSymbol Null { get; } = new IntrinsicTypeSymbol(TypeKind.Null, "<null>");

    internal static TypeSymbol Error { get; } = new IntrinsicTypeSymbol(TypeKind.Error, "<error>");

    /// <summary>Creates a nullable type.</summary>
    /// <param name="underlyingType">The underlying non-nullable type.</param>
    /// <returns>The nullable type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingType" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the type is already nullable or cannot be nullable.</exception>
    public static NullableTypeSymbol Nullable(TypeSymbol underlyingType)
    {
        return underlyingType switch
        {
            null =>
                throw new ArgumentNullException(nameof(underlyingType)),
            NullableTypeSymbol =>
                throw new ArgumentException("A nullable type cannot be nullable again.", nameof(underlyingType)),
            { Kind: TypeKind.Void or TypeKind.Null or TypeKind.Error } =>
                throw new ArgumentException(
                    $"Type '{underlyingType.DisplayName}' cannot be nullable.",
                    nameof(underlyingType)
                ),
            _ => new NullableTypeSymbol(underlyingType),
        };
    }

    /// <summary>Creates an array type.</summary>
    /// <param name="elementType">The array element type.</param>
    /// <returns>The array type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the type cannot be used as an array element.</exception>
    public static ArrayTypeSymbol Array(TypeSymbol elementType)
    {
        if (elementType is null)
        {
            throw new ArgumentNullException(nameof(elementType));
        }

        if (elementType.Kind is TypeKind.Void or TypeKind.Null or TypeKind.Error)
        {
            throw new ArgumentException(
                $"Type '{elementType.DisplayName}' cannot be used as an array element.",
                nameof(elementType)
            );
        }

        return new ArrayTypeSymbol(elementType);
    }
}
