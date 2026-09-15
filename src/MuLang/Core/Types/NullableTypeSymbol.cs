namespace MuLang.Core.Types;

/// <summary>Represents a nullable MuLang type.</summary>
public sealed class NullableTypeSymbol : TypeSymbol
{
    internal NullableTypeSymbol(TypeSymbol underlyingType)
        : base(TypeKind.Nullable)
    {
        UnderlyingType = underlyingType;
    }

    /// <summary>Gets the underlying non-nullable type.</summary>
    public TypeSymbol UnderlyingType { get; }

    /// <inheritdoc />
    public override string DisplayName => $"{UnderlyingType.DisplayName}?";
}
