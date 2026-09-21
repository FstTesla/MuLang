namespace MuLang.Core.Types;

/// <summary>Represents a MuLang type.</summary>
public abstract class TypeSymbol
{
    private protected TypeSymbol(TypeKind kind)
    {
        Kind = kind;
    }

    /// <summary>Gets the type kind.</summary>
    public TypeKind Kind { get; }

    /// <summary>Gets the language-facing display name.</summary>
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public sealed override string ToString() => DisplayName;
}
