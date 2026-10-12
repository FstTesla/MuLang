using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class UserParameterSymbol : BoundVariableSymbol
{
    public UserParameterSymbol(
        string name,
        TypeSymbol type,
        int slot,
        TextSpan declarationSpan
    )
        : base(name, type, slot)
    {
        DeclarationSpan = declarationSpan;
    }

    public TextSpan DeclarationSpan { get; }
}
