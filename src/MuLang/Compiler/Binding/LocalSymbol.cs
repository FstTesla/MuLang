using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class LocalSymbol : BoundVariableSymbol
{
    public LocalSymbol(string name, TypeSymbol type, int slot)
        : base(name, type, slot)
    {
    }
}
