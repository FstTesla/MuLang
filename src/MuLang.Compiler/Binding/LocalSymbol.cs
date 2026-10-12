using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class LocalSymbol : BoundVariableSymbol
{
    public LocalSymbol(
        string name,
        TypeSymbol type,
        int slot,
        bool isReadOnly,
        int declarationLoopDepth,
        TextSpan? declarationSpan
    )
        : base(name, type, slot)
    {
        IsReadOnly = isReadOnly;
        DeclarationLoopDepth = declarationLoopDepth;
        DeclarationSpan = declarationSpan;
    }

    public bool IsReadOnly { get; }

    public int DeclarationLoopDepth { get; }

    public TextSpan? DeclarationSpan { get; }
}
