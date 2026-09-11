using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

public sealed class GlobalSymbol
{
    public GlobalSymbol(string id, string name, TypeSymbol type)
    {
        SymbolValidation.ValidateId(id, nameof(id));
        LanguageNames.ValidateIdentifier(name, nameof(name));
        SymbolValidation.ValidateValueType(type, nameof(type));

        Id = id;
        Name = name;
        Type = type;
    }

    public string Id { get; }

    public string Name { get; }

    public TypeSymbol Type { get; }
}
