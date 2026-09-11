using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core.Environment;

public sealed class EnvironmentSchema
{
    private readonly IReadOnlyDictionary<string, ObjectTypeSymbol> typesByName;
    private readonly IReadOnlyDictionary<string, GlobalSymbol> globalsByName;
    private readonly IReadOnlyDictionary<string, GlobalSymbol> globalsById;
    private readonly IReadOnlyDictionary<string, FunctionSymbol> functionsByName;
    private readonly IReadOnlyDictionary<string, FunctionSymbol> functionsById;

    internal EnvironmentSchema(
        LanguageVersion languageVersion,
        ObjectTypeSymbol[] types,
        GlobalSymbol[] globals,
        FunctionSymbol[] functions,
        EnvironmentFingerprint fingerprint
    )
    {
        LanguageVersion = languageVersion;
        Types = Array.AsReadOnly(types);
        Globals = Array.AsReadOnly(globals);
        Functions = Array.AsReadOnly(functions);
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

    public LanguageVersion LanguageVersion { get; }

    public IReadOnlyCollection<ObjectTypeSymbol> Types { get; }

    public IReadOnlyCollection<GlobalSymbol> Globals { get; }

    public IReadOnlyCollection<FunctionSymbol> Functions { get; }

    public EnvironmentFingerprint Fingerprint { get; }

    public bool TryGetType(
        string name,
        [NotNullWhen(true)] out ObjectTypeSymbol? type
    )
    {
        return typesByName.TryGetValue(name, out type);
    }

    public bool TryGetGlobal(
        string name,
        [NotNullWhen(true)] out GlobalSymbol? global
    )
    {
        return globalsByName.TryGetValue(name, out global);
    }

    public bool TryGetFunction(
        string name,
        [NotNullWhen(true)] out FunctionSymbol? function
    )
    {
        return functionsByName.TryGetValue(name, out function);
    }

    public bool TryGetGlobalById(
        string id,
        [NotNullWhen(true)] out GlobalSymbol? global
    )
    {
        return globalsById.TryGetValue(id, out global);
    }

    public bool TryGetFunctionById(
        string id,
        [NotNullWhen(true)] out FunctionSymbol? function
    )
    {
        return functionsById.TryGetValue(id, out function);
    }
}
