using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class LocalSymbol : BoundVariableSymbol
{
    public LocalSymbol(
        string name,
        TypeSymbol type,
        int slot,
        bool isReadOnly,
        int declarationLoopDepth
    )
        : base(name, type, slot)
    {
        IsReadOnly = isReadOnly;
        DeclarationLoopDepth = declarationLoopDepth;
    }

    public bool IsReadOnly { get; }

    public int DeclarationLoopDepth { get; }
}
