namespace MuLang.Core.Types;

/// <summary>Represents a MuLang array type.</summary>
public sealed class ArrayTypeSymbol : TypeSymbol
{
    internal ArrayTypeSymbol(TypeSymbol elementType)
        : base(TypeKind.Array)
    {
        ElementType = elementType;
    }

    /// <summary>Gets the array element type.</summary>
    public TypeSymbol ElementType { get; }

    /// <inheritdoc />
    public override string DisplayName => $"{ElementType.DisplayName}[]";
}
