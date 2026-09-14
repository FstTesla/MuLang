using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class UserParameterSymbol : BoundVariableSymbol
{
    public UserParameterSymbol(string name, TypeSymbol type, int slot)
        : base(name, type, slot)
    {
    }
}
