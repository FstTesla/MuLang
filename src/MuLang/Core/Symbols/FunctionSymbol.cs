using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

/// <summary>Represents a function exposed by a MuLang environment.</summary>
public sealed class FunctionSymbol
{
    /// <summary>Initializes a new instance of the <see cref="FunctionSymbol" /> class.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="name">The language name.</param>
    /// <param name="parameters">The function parameters.</param>
    /// <param name="returnType">The function return type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters" /> or <paramref name="returnType" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol value is invalid or a parameter name is duplicated.</exception>
    public FunctionSymbol(
        string id,
        string name,
        IEnumerable<ParameterSymbol> parameters,
        TypeSymbol returnType
    )
    {
        SymbolValidation.ValidateId(id, nameof(id));
        LanguageNames.ValidateIdentifier(name, nameof(name));

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (returnType is null)
        {
            throw new ArgumentNullException(nameof(returnType));
        }

        if (returnType.Kind is TypeKind.Null or TypeKind.Error)
        {
            throw new ArgumentException(
                $"Type '{returnType.DisplayName}' cannot be used as a function return type.",
                nameof(returnType)
            );
        }

        IReadOnlyList<ParameterSymbol> parameterList = [ .. parameters ];

        IGrouping<string, ParameterSymbol>? duplicate = parameterList
            .GroupBy(static parameter => parameter.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Parameter '{duplicate.Key}' is declared more than once.",
                nameof(parameters)
            );
        }

        Id = id;
        Name = name;
        Parameters = parameterList;
        ReturnType = returnType;
    }

    /// <summary>Gets the provider identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the language name.</summary>
    public string Name { get; }

    /// <summary>Gets the ordered function parameters.</summary>
    public IReadOnlyList<ParameterSymbol> Parameters { get; }

    /// <summary>Gets the return type.</summary>
    public TypeSymbol ReturnType { get; }
}
