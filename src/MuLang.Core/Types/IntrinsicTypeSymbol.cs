namespace MuLang.Core.Types;

internal sealed class IntrinsicTypeSymbol : TypeSymbol
{
    public IntrinsicTypeSymbol(TypeKind kind, string displayName)
        : base(kind)
    {
        DisplayName = displayName;
    }

    public override string DisplayName { get; }
}
