using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary;

/// <summary>Composes selected standard-library modules into MuLang environments.</summary>
public static class StandardLibraryComposer
{
    /// <summary>Creates an environment from selected modules.</summary>
    /// <param name="modules">The explicitly selected modules.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modules" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when modules are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(
        IEnumerable<StandardLibraryModule> modules
    )
    {
        return ComposeCore(null, modules, LanguageVersion.Version1_1);
    }

    /// <summary>Creates an environment from selected modules.</summary>
    /// <param name="modules">The explicitly selected modules.</param>
    /// <param name="languageVersion">The target language version.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modules" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="languageVersion" /> is not defined.</exception>
    /// <exception cref="InvalidOperationException">Thrown when modules are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(
        IEnumerable<StandardLibraryModule> modules,
        LanguageVersion languageVersion
    )
    {
        return ComposeCore(null, modules, languageVersion);
    }

    /// <summary>Creates an environment by adding selected modules to a host schema.</summary>
    /// <param name="host">The host environment schema.</param>
    /// <param name="modules">The explicitly selected modules.</param>
    /// <returns>The composed environment schema.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when modules are incompatible or declarations collide.</exception>
    public static EnvironmentSchema Compose(
        EnvironmentSchema host,
        IEnumerable<StandardLibraryModule> modules
    )
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        return ComposeCore(host, modules, host.LanguageVersion);
    }

    private static EnvironmentSchema ComposeCore(
        EnvironmentSchema? host,
        IEnumerable<StandardLibraryModule> modules,
        LanguageVersion languageVersion
    )
    {
        IReadOnlyList<StandardLibraryModule> selected = ValidateModules(modules, languageVersion);
        EnvironmentBuilder builder = new ();
        DeclarationRegistry registry = new ();

        if (host is not null)
        {
            AddHost(builder, registry, host);
        }

        ValidateDeclarations(registry, selected);
        AddModules(builder, selected);
        return builder.Build(languageVersion);
    }

    private static IReadOnlyList<StandardLibraryModule> ValidateModules(
        IEnumerable<StandardLibraryModule> modules,
        LanguageVersion languageVersion
    )
    {
        if (modules is null)
        {
            throw new ArgumentNullException(nameof(modules));
        }

        if (!Enum.IsDefined(languageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(languageVersion));
        }

        StandardLibraryModule[] selected = [ .. modules ];

        if (selected.Any(static module => module is null))
        {
            throw new ArgumentException("Selected modules cannot contain null values.", nameof(modules));
        }

        IDictionary<string, StandardLibraryModule> modulesById =
            new Dictionary<string, StandardLibraryModule>(StringComparer.Ordinal);

        foreach (StandardLibraryModule module in selected)
        {
            if (modulesById.TryGetValue(module.Id, out StandardLibraryModule? existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate module identifier '{module.Id}' between modules '{existing.DisplayName}' and '{module.DisplayName}'."
                );
            }

            modulesById.Add(module.Id, module);

            if (languageVersion < module.MinimumLanguageVersion)
            {
                throw new InvalidOperationException(
                    $"Module '{module.DisplayName}' ({module.Id}) requires language version {module.MinimumLanguageVersion}, but {languageVersion} was selected."
                );
            }
        }

        ValidateDeclarations(new DeclarationRegistry(), selected);
        return selected;
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

    private static void ValidateDeclarations(
        DeclarationRegistry registry,
        IReadOnlyList<StandardLibraryModule> modules
    )
    {
        foreach (StandardLibraryModule module in modules)
        {
            foreach (ObjectTypeSymbol type in module.Types)
            {
                registry.AddType(type, Describe(module, "type", type.Name));
            }

            foreach (GlobalSymbol global in module.Globals)
            {
                registry.AddGlobal(global, Describe(module, "global", global.Name));
            }

            foreach (FunctionSymbol function in module.Functions)
            {
                registry.AddFunction(function, Describe(module, "function", function.Name));
            }
        }
    }

    private static string Describe(
        StandardLibraryModule module,
        string declarationKind,
        string declarationName
    )
    {
        return $"module '{module.DisplayName}' ({module.Id}) {declarationKind} '{declarationName}'";
    }

    private static void AddModules(
        EnvironmentBuilder builder,
        IReadOnlyList<StandardLibraryModule> modules
    )
    {
        foreach (StandardLibraryModule module in modules)
        {
            foreach (ObjectTypeSymbol type in module.Types)
            {
                builder.AddType(type);
            }

            foreach (GlobalSymbol global in module.Globals)
            {
                builder.AddGlobal(global.Id, global.Name, global.Type);
            }

            foreach (FunctionSymbol function in module.Functions)
            {
                builder.AddFunction(
                    function.Id,
                    function.Name,
                    function.Parameters,
                    function.ReturnType
                );
            }
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
