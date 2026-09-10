namespace MuLang.Core.Types;

public abstract class TypeSymbol
{
    private protected TypeSymbol(TypeKind kind)
    {
        Kind = kind;
    }

    public TypeKind Kind { get; }

    public abstract string DisplayName { get; }

    public sealed override string ToString() => DisplayName;
}
