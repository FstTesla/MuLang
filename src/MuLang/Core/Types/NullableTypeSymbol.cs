namespace MuLang.Core.Types;

public sealed class NullableTypeSymbol : TypeSymbol
{
    internal NullableTypeSymbol(TypeSymbol underlyingType)
        : base(TypeKind.Nullable)
    {
        UnderlyingType = underlyingType;
    }

    public TypeSymbol UnderlyingType { get; }

    public override string DisplayName => $"{UnderlyingType.DisplayName}?";
}
