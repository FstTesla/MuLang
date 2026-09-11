namespace MuLang.Core.Types;

public static class TypeSymbols
{
    public static TypeSymbol Bool { get; } = new IntrinsicTypeSymbol(TypeKind.Bool, "bool");

    public static TypeSymbol Int { get; } = new IntrinsicTypeSymbol(TypeKind.Int, "int");

    public static TypeSymbol Number { get; } = new IntrinsicTypeSymbol(TypeKind.Number, "number");

    public static TypeSymbol String { get; } = new IntrinsicTypeSymbol(TypeKind.String, "string");

    public static TypeSymbol Unknown { get; } = new IntrinsicTypeSymbol(
        TypeKind.Unknown,
        "unknown"
    );

    public static TypeSymbol Object { get; } = new IntrinsicTypeSymbol(
        TypeKind.Object,
        "object"
    );

    public static TypeSymbol Void { get; } = new IntrinsicTypeSymbol(TypeKind.Void, "void");

    internal static TypeSymbol Null { get; } = new IntrinsicTypeSymbol(TypeKind.Null, "<null>");

    internal static TypeSymbol Error { get; } = new IntrinsicTypeSymbol(TypeKind.Error, "<error>");

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
