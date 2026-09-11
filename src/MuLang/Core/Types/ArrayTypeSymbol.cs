namespace MuLang.Core.Types;

public sealed class ArrayTypeSymbol : TypeSymbol
{
    internal ArrayTypeSymbol(TypeSymbol elementType)
        : base(TypeKind.Array)
    {
        ElementType = elementType;
    }

    public TypeSymbol ElementType { get; }

    public override string DisplayName => $"{ElementType.DisplayName}[]";
}
