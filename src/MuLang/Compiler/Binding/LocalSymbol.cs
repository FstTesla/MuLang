using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class LocalSymbol
{
    public LocalSymbol(string name, TypeSymbol type, int slot)
    {
        Name = name;
        Type = type;
        Slot = slot;
    }

    public string Name { get; }

    public TypeSymbol Type { get; }

    public int Slot { get; }
}
