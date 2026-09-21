using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core.Environment;

/// <summary>Represents the types, globals, and functions available to MuLang code.</summary>
public sealed class EnvironmentSchema
{
    private readonly IReadOnlyDictionary<string, ObjectTypeSymbol> typesByName;
    private readonly IReadOnlyDictionary<string, GlobalSymbol> globalsByName;
    private readonly IReadOnlyDictionary<string, GlobalSymbol> globalsById;
    private readonly IReadOnlyDictionary<string, FunctionSymbol> functionsByName;
    private readonly IReadOnlyDictionary<string, FunctionSymbol> functionsById;

    internal EnvironmentSchema(
        LanguageVersion languageVersion,
        IReadOnlyCollection<ObjectTypeSymbol> types,
        IReadOnlyCollection<GlobalSymbol> globals,
        IReadOnlyCollection<FunctionSymbol> functions,
        EnvironmentFingerprint fingerprint
    )
    {
        LanguageVersion = languageVersion;
        Types = types;
        Globals = globals;
        Functions = functions;
        Fingerprint = fingerprint;
        typesByName = types.ToFrozenDictionary(
            static type => type.Name,
            StringComparer.Ordinal
        );
        globalsByName = globals.ToFrozenDictionary(
            static global => global.Name,
            StringComparer.Ordinal
        );
        globalsById = globals.ToFrozenDictionary(
            static global => global.Id,
            StringComparer.Ordinal
        );
        functionsByName = functions.ToFrozenDictionary(
            static function => function.Name,
            StringComparer.Ordinal
        );
        functionsById = functions.ToFrozenDictionary(
            static function => function.Id,
            StringComparer.Ordinal
        );
    }

    /// <summary>Gets the language version.</summary>
    public LanguageVersion LanguageVersion { get; }

    /// <summary>Gets the registered structured types.</summary>
    public IReadOnlyCollection<ObjectTypeSymbol> Types { get; }

    /// <summary>Gets the registered global symbols.</summary>
    public IReadOnlyCollection<GlobalSymbol> Globals { get; }

    /// <summary>Gets the registered function symbols.</summary>
    public IReadOnlyCollection<FunctionSymbol> Functions { get; }

    /// <summary>Gets the stable fingerprint of the schema.</summary>
    public EnvironmentFingerprint Fingerprint { get; }

    /// <summary>Gets a structured type by its language name.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="type">When this method returns, contains the type, if found.</param>
    /// <returns><c>true</c> if a type with the specified name was found; otherwise, <c>false</c>.</returns>
    public bool TryGetType(
        string name,
        [NotNullWhen(true)] out ObjectTypeSymbol? type
    )
    {
        return typesByName.TryGetValue(name, out type);
    }

    /// <summary>Gets a global symbol by its language name.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="global">When this method returns, contains the global symbol, if found.</param>
    /// <returns><c>true</c> if a global with the specified name was found; otherwise, <c>false</c>.</returns>
    public bool TryGetGlobal(
        string name,
        [NotNullWhen(true)] out GlobalSymbol? global
    )
    {
        return globalsByName.TryGetValue(name, out global);
    }

    /// <summary>Gets a function symbol by its language name.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="function">When this method returns, contains the function symbol, if found.</param>
    /// <returns><c>true</c> if a function with the specified name was found; otherwise, <c>false</c>.</returns>
    public bool TryGetFunction(
        string name,
        [NotNullWhen(true)] out FunctionSymbol? function
    )
    {
        return functionsByName.TryGetValue(name, out function);
    }

    /// <summary>Gets a global symbol by its provider identifier.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="global">When this method returns, contains the global symbol, if found.</param>
    /// <returns><c>true</c> if a global with the specified identifier was found; otherwise, <c>false</c>.</returns>
    public bool TryGetGlobalById(
        string id,
        [NotNullWhen(true)] out GlobalSymbol? global
    )
    {
        return globalsById.TryGetValue(id, out global);
    }

    /// <summary>Gets a function symbol by its provider identifier.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="function">When this method returns, contains the function symbol, if found.</param>
    /// <returns><c>true</c> if a function with the specified identifier was found; otherwise, <c>false</c>.</returns>
    public bool TryGetFunctionById(
        string id,
        [NotNullWhen(true)] out FunctionSymbol? function
    )
    {
        return functionsById.TryGetValue(id, out function);
    }
}
