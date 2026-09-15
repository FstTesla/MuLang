using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

/// <summary>Represents a parameter of a function exposed by a MuLang environment.</summary>
public sealed class ParameterSymbol
{
    /// <summary>Initializes a new instance of the <see cref="ParameterSymbol" /> class.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="type">The type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol value is invalid.</exception>
    public ParameterSymbol(string name, TypeSymbol type)
    {
        LanguageNames.ValidateIdentifier(name, nameof(name));
        SymbolValidation.ValidateValueType(type, nameof(type));

        Name = name;
        Type = type;
    }

    /// <summary>Gets the parameter name.</summary>
    public string Name { get; }

    /// <summary>Gets the parameter type.</summary>
    public TypeSymbol Type { get; }
}
