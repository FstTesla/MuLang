using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary;

/// <summary>Composes selected standard-library symbols into MuLang environments.</summary>
public static class StandardLibraryComposer
{
    /// <summary>Creates an environment from a symbol selection.</summary>
    /// <param name="selection">The normalized symbol selection.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when symbols are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(StandardLibrarySelection selection)
    {
        return ComposeCore(null, selection, LanguageVersion.Version1_1);
    }

    /// <summary>Creates an environment from a symbol selection.</summary>
    /// <param name="selection">The normalized symbol selection.</param>
    /// <param name="languageVersion">The target language version.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="languageVersion" /> is not defined.</exception>
    /// <exception cref="InvalidOperationException">Thrown when symbols are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(
        StandardLibrarySelection selection,
        LanguageVersion languageVersion
    )
    {
        return ComposeCore(null, selection, languageVersion);
    }

    /// <summary>Creates an environment by adding a symbol selection to a host schema.</summary>
    /// <param name="host">The host environment schema.</param>
    /// <param name="selection">The normalized symbol selection.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when symbols are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(
        EnvironmentSchema host,
        StandardLibrarySelection selection
    )
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        return ComposeCore(host, selection, host.LanguageVersion);
    }

    private static EnvironmentSchema ComposeCore(
        EnvironmentSchema? host,
        StandardLibrarySelection selection,
        LanguageVersion languageVersion
    )
    {
        if (selection is null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (!Enum.IsDefined(languageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(languageVersion));
        }

        foreach (IStandardLibrarySymbol symbol in selection.Symbols)
        {
            if (languageVersion < symbol.MinimumLanguageVersion)
            {
                throw new InvalidOperationException(
                    $"Standard-library symbol '{symbol.Name}' ({symbol.Id}) requires language version {symbol.MinimumLanguageVersion}, but {languageVersion} was selected."
                );
            }
        }

        EnvironmentBuilder builder = new ();
        DeclarationRegistry registry = new ();

        if (host is not null)
        {
            AddHost(builder, registry, host);
        }

        ValidateSelection(registry, selection);
        AddSelection(builder, selection);
        return builder.Build(languageVersion);
    }

    private static void AddHost(
        EnvironmentBuilder builder,
        DeclarationRegistry registry,
        EnvironmentSchema host
    )
    {
        foreach (ObjectTypeSymbol type in host.Types)
        {
            registry.AddType(type, $"host type '{type.Name}'");
            builder.AddType(type);
        }

        foreach (GlobalSymbol global in host.Globals)
        {
            registry.AddGlobal(global, $"host global '{global.Name}'");
            builder.AddGlobal(global.Id, global.Name, global.Type);
        }

        foreach (FunctionSymbol function in host.Functions)
        {
            registry.AddFunction(function, $"host function '{function.Name}'");
            builder.AddFunction(function.Id, function.Name, function.Parameters, function.ReturnType);
        }
    }

    private static void ValidateSelection(
        DeclarationRegistry registry,
        StandardLibrarySelection selection
    )
    {
        foreach (StandardLibraryType type in selection.Types)
        {
            registry.AddType(type.Declaration, Describe("type", type));
        }

        foreach (StandardLibraryGlobal global in selection.Globals)
        {
            registry.AddGlobal(global.Declaration, Describe("global", global));
        }

        foreach (StandardLibraryFunction function in selection.Functions)
        {
            registry.AddFunction(function.Declaration, Describe("function", function));
        }
    }

    private static string Describe(string declarationKind, IStandardLibrarySymbol symbol)
    {
        return $"standard-library {declarationKind} '{symbol.Name}' ({symbol.Id})";
    }

    private static void AddSelection(
        EnvironmentBuilder builder,
        StandardLibrarySelection selection
    )
    {
        foreach (StandardLibraryType type in selection.Types)
        {
            builder.AddType(type.Declaration);
        }

        foreach (StandardLibraryGlobal global in selection.Globals)
        {
            GlobalSymbol declaration = global.Declaration;
            builder.AddGlobal(declaration.Id, declaration.Name, declaration.Type);
        }

        foreach (StandardLibraryFunction function in selection.Functions)
        {
            FunctionSymbol declaration = function.Declaration;
            builder.AddFunction(
                declaration.Id,
                declaration.Name,
                declaration.Parameters,
                declaration.ReturnType
            );
        }
    }

    private sealed class DeclarationRegistry
    {
        private readonly IDictionary<string, string> functionNames =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IDictionary<string, string> globalNames =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IDictionary<string, string> providerIds =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IDictionary<string, string> typeNames =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void AddType(ObjectTypeSymbol type, string description)
        {
            Add(typeNames, type.Name, "type name", description);
            Add(providerIds, type.Id!, "provider identifier", description);
        }

        public void AddGlobal(GlobalSymbol global, string description)
        {
            Add(globalNames, global.Name, "global name", description);
            Add(providerIds, global.Id, "provider identifier", description);
        }

        public void AddFunction(FunctionSymbol function, string description)
        {
            Add(functionNames, function.Name, "function name", description);
            Add(providerIds, function.Id, "provider identifier", description);
        }

        private static void Add(
            IDictionary<string, string> declarations,
            string value,
            string valueKind,
            string description
        )
        {
            if (declarations.TryGetValue(value, out string? existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate {valueKind} '{value}' between {existing} and {description}."
                );
            }

            declarations.Add(value, description);
        }
    }
}
