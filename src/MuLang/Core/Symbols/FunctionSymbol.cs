using MuLang.Core.Types;

namespace MuLang.Core.Symbols;

public sealed class FunctionSymbol
{
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

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<ParameterSymbol> Parameters { get; }

    public TypeSymbol ReturnType { get; }
}
