using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

/// <summary>Represents a global value exposed by a MuLang environment.</summary>
public sealed class GlobalSymbol
{
    /// <summary>Initializes a new instance of the <see cref="GlobalSymbol" /> class.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="name">The language name.</param>
    /// <param name="type">The type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol value is invalid.</exception>
    public GlobalSymbol(string id, string name, TypeSymbol type)
    {
        SymbolValidation.ValidateId(id, nameof(id));
        LanguageNames.ValidateIdentifier(name, nameof(name));
        SymbolValidation.ValidateValueType(type, nameof(type));

        Id = id;
        Name = name;
        Type = type;
    }

    /// <summary>Gets the provider identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the language name.</summary>
    public string Name { get; }

    /// <summary>Gets the global value type.</summary>
    public TypeSymbol Type { get; }
}
