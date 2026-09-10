using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Core.Environment;

public sealed class EnvironmentSchema
{
    private readonly FrozenDictionary<string, ObjectTypeSymbol> typesByName;
    private readonly FrozenDictionary<string, GlobalSymbol> globalsByName;
    private readonly FrozenDictionary<string, FunctionSymbol> functionsByName;

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
        functionsByName = functions.ToFrozenDictionary(
            static function => function.Name,
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
}
