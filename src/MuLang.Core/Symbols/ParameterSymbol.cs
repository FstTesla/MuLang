using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

public sealed class ParameterSymbol
{
    public ParameterSymbol(string name, TypeSymbol type)
    {
        LanguageNames.ValidateIdentifier(name, nameof(name));
        SymbolValidation.ValidateValueType(type, nameof(type));

        Name = name;
        Type = type;
    }

    public string Name { get; }

    public TypeSymbol Type { get; }
}
