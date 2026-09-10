using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Core.Environment;

public sealed class EnvironmentBuilder
{
    private readonly Dictionary<string, ObjectTypeSymbol> typesByName =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ObjectTypeSymbol> typesById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GlobalSymbol> globalsByName =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, GlobalSymbol> globalsById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionSymbol> functionsByName =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionSymbol> functionsById =
        new(StringComparer.Ordinal);

    public EnvironmentBuilder AddType(ObjectTypeSymbol type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type.Id is null)
        {
            throw new ArgumentException(
                "Anonymous structured types cannot be registered in an environment.",
                nameof(type)
            );
        }

        EnsureUnique(typesByName, type.Name, "type name");
        EnsureUnique(typesById, type.Id, "type identifier");
        typesByName.Add(type.Name, type);
        typesById.Add(type.Id, type);

        return this;
    }

    public EnvironmentBuilder AddGlobal(string id, string name, TypeSymbol type)
    {
        GlobalSymbol global = new(id, name, type);
        EnsureUnique(globalsByName, global.Name, "global name");
        EnsureUnique(globalsById, global.Id, "global identifier");
        globalsByName.Add(global.Name, global);
        globalsById.Add(global.Id, global);

        return this;
    }

    public EnvironmentBuilder AddFunction(
        string id,
        string name,
        IEnumerable<ParameterSymbol> parameters,
        TypeSymbol returnType
    )
    {
        FunctionSymbol function = new(id, name, parameters, returnType);
        EnsureUnique(functionsByName, function.Name, "function name");
        EnsureUnique(functionsById, function.Id, "function identifier");
        functionsByName.Add(function.Name, function);
        functionsById.Add(function.Id, function);

        return this;
    }

    public EnvironmentSchema Build(LanguageVersion languageVersion = LanguageVersion.Version1)
    {
        if (!Enum.IsDefined(languageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(languageVersion));
        }

        ObjectTypeSymbol[] types = typesByName.Values
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToArray();
        GlobalSymbol[] globals = globalsByName.Values
            .OrderBy(static global => global.Name, StringComparer.Ordinal)
            .ToArray();
        FunctionSymbol[] functions = functionsByName.Values
            .OrderBy(static function => function.Name, StringComparer.Ordinal)
            .ToArray();
        ValidateReferencedTypes(types, globals, functions);
        EnvironmentFingerprint fingerprint = EnvironmentFingerprintFactory.Create(
            languageVersion,
            types,
            globals,
            functions
        );

        return new EnvironmentSchema(
            languageVersion,
            types,
            globals,
            functions,
            fingerprint
        );
    }

    private void ValidateReferencedTypes(
        IReadOnlyCollection<ObjectTypeSymbol> types,
        IReadOnlyCollection<GlobalSymbol> globals,
        IReadOnlyCollection<FunctionSymbol> functions
    )
    {
        HashSet<ObjectTypeSymbol> visited = new(ReferenceEqualityComparer.Instance);

        foreach (ObjectTypeSymbol type in types)
        {
            ValidateReferencedType(type, visited);
        }

        foreach (GlobalSymbol global in globals)
        {
            ValidateReferencedType(global.Type, visited);
        }

        foreach (FunctionSymbol function in functions)
        {
            foreach (ParameterSymbol parameter in function.Parameters)
            {
                ValidateReferencedType(parameter.Type, visited);
            }

            ValidateReferencedType(function.ReturnType, visited);
        }
    }

    private void ValidateReferencedType(
        TypeSymbol type,
        ISet<ObjectTypeSymbol> visited
    )
    {
        switch (type)
        {
            case NullableTypeSymbol nullable:
            {
                ValidateReferencedType(nullable.UnderlyingType, visited);
                break;
            }
            case ArrayTypeSymbol array:
            {
                ValidateReferencedType(array.ElementType, visited);
                break;
            }
            case ObjectTypeSymbol structuredObject:
            {
                if (!visited.Add(structuredObject))
                {
                    break;
                }

                if (
                    structuredObject.Id is null ||
                    !typesById.TryGetValue(
                        structuredObject.Id,
                        out ObjectTypeSymbol? registeredType
                    ) ||
                    !ReferenceEquals(structuredObject, registeredType)
                )
                {
                    throw new InvalidOperationException(
                        $"Structured type '{structuredObject.DisplayName}' must be registered using the same instance referenced by the environment schema."
                    );
                }

                foreach (ObjectPropertySymbol property in structuredObject.Properties)
                {
                    ValidateReferencedType(property.Type, visited);
                }

                break;
            }
        }
    }

    private static void EnsureUnique<T>(
        IReadOnlyDictionary<string, T> items,
        string key,
        string description
    )
    {
        if (items.ContainsKey(key))
        {
            throw new ArgumentException($"Duplicate {description} '{key}'.");
        }
    }
}
