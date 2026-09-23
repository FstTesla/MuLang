namespace MuLang.Core.Types;

/// <summary>Represents a MuLang array type.</summary>
public sealed class ArrayTypeSymbol : TypeSymbol
{
    internal ArrayTypeSymbol(TypeSymbol elementType, bool isReadOnly)
        : base(TypeKind.Array)
    {
        ElementType = elementType;
        IsReadOnly = isReadOnly;
    }

    /// <summary>Gets the array element type.</summary>
    public TypeSymbol ElementType { get; }

    /// <summary>Gets a value indicating whether the array exposes read-only capability.</summary>
    public bool IsReadOnly { get; }

    /// <inheritdoc />
    public override string DisplayName => $"{ElementType.DisplayName}[]{(IsReadOnly ? "$" : "")}";
}
