using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Core.Environment;

/// <summary>Builds an environment schema for MuLang compilation and execution.</summary>
public sealed class EnvironmentBuilder
{
    private readonly IDictionary<string, ObjectTypeSymbol> typesByName =
        new Dictionary<string, ObjectTypeSymbol>(StringComparer.Ordinal);

    private readonly IDictionary<string, ObjectTypeSymbol> typesById =
        new Dictionary<string, ObjectTypeSymbol>(StringComparer.Ordinal);

    private readonly IDictionary<string, GlobalSymbol> globalsByName =
        new Dictionary<string, GlobalSymbol>(StringComparer.Ordinal);

    private readonly IDictionary<string, GlobalSymbol> globalsById =
        new Dictionary<string, GlobalSymbol>(StringComparer.Ordinal);

    private readonly IDictionary<string, FunctionSymbol> functionsByName =
        new Dictionary<string, FunctionSymbol>(StringComparer.Ordinal);

    private readonly IDictionary<string, FunctionSymbol> functionsById =
        new Dictionary<string, FunctionSymbol>(StringComparer.Ordinal);

    /// <summary>Adds a structured type to the environment.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The same <see cref="EnvironmentBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the type is anonymous or its name or identifier is already registered.</exception>
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

    /// <summary>Adds a global value to the environment.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="name">The language name.</param>
    /// <param name="type">The type.</param>
    /// <returns>The same <see cref="EnvironmentBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol value is invalid or its name or identifier is already registered.</exception>
    public EnvironmentBuilder AddGlobal(string id, string name, TypeSymbol type)
    {
        GlobalSymbol global = new (id, name, type);
        EnsureUnique(globalsByName, global.Name, "global name");
        EnsureUnique(globalsById, global.Id, "global identifier");
        globalsByName.Add(global.Name, global);
        globalsById.Add(global.Id, global);

        return this;
    }

    /// <summary>Adds a provider function to the environment.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="name">The language name.</param>
    /// <param name="parameters">The function parameters.</param>
    /// <param name="returnType">The function return type.</param>
    /// <returns>The same <see cref="EnvironmentBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters" /> or <paramref name="returnType" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol value is invalid or its name or identifier is already registered.</exception>
    public EnvironmentBuilder AddFunction(
        string id,
        string name,
        IEnumerable<ParameterSymbol> parameters,
        TypeSymbol returnType
    )
    {
        FunctionSymbol function = new (id, name, parameters, returnType);
        EnsureUnique(functionsByName, function.Name, "function name");
        EnsureUnique(functionsById, function.Id, "function identifier");
        functionsByName.Add(function.Name, function);
        functionsById.Add(function.Id, function);

        return this;
    }

    /// <summary>Creates an immutable environment schema from the registered symbols.</summary>
    /// <param name="languageVersion">The language version.</param>
    /// <returns>The immutable environment schema.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="languageVersion" /> is not defined.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a referenced structured type is not registered by the same instance.</exception>
    public EnvironmentSchema Build(LanguageVersion languageVersion = LanguageVersion.Version1_1)
    {
        if (!Enum.IsDefined(languageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(languageVersion));
        }

        IReadOnlyCollection<ObjectTypeSymbol> types =
            [ .. typesByName.Values.OrderBy(static type => type.Name, StringComparer.Ordinal) ];
        IReadOnlyCollection<GlobalSymbol> globals =
            [ .. globalsByName.Values.OrderBy(static global => global.Name, StringComparer.Ordinal) ];
        IReadOnlyCollection<FunctionSymbol> functions =
            [ .. functionsByName.Values.OrderBy(static function => function.Name, StringComparer.Ordinal) ];
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
        ISet<ObjectTypeSymbol> visited = new HashSet<ObjectTypeSymbol>(ReferenceEqualityComparer.Instance);

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
        IDictionary<string, T> items,
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
