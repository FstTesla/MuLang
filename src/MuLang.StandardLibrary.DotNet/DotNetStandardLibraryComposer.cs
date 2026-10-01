using MuLang.Core.Symbols;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;
using System.Collections.Frozen;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Composes and validates selected .NET standard-library bindings.</summary>
public static class DotNetStandardLibraryComposer
{
    /// <summary>Composes selected standard-library bindings.</summary>
    /// <param name="bindings">The selected bindings.</param>
    /// <returns>The validated runtime composition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bindings" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when declaration parity is invalid or implementations collide.</exception>
    public static DotNetStandardLibraryComposition Compose(
        IEnumerable<DotNetStandardLibraryModuleBinding> bindings
    )
    {
        return Compose(bindings, [ ], [ ]);
    }

    /// <summary>Composes selected standard-library bindings with host runtime values and functions.</summary>
    /// <param name="bindings">The selected bindings.</param>
    /// <param name="hostGlobals">The host globals keyed by provider identifier.</param>
    /// <param name="hostFunctions">The host functions keyed by provider identifier.</param>
    /// <returns>The validated runtime composition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when declaration parity is invalid or implementations collide.</exception>
    public static DotNetStandardLibraryComposition Compose(
        IEnumerable<DotNetStandardLibraryModuleBinding> bindings,
        IEnumerable<KeyValuePair<string, object?>> hostGlobals,
        IEnumerable<KeyValuePair<string, DotNetProviderFunction>> hostFunctions
    )
    {
        if (bindings is null)
        {
            throw new ArgumentNullException(nameof(bindings));
        }

        if (hostGlobals is null)
        {
            throw new ArgumentNullException(nameof(hostGlobals));
        }

        if (hostFunctions is null)
        {
            throw new ArgumentNullException(nameof(hostFunctions));
        }

        DotNetStandardLibraryModuleBinding[] selected = [ .. bindings ];

        if (selected.Any(static binding => binding is null))
        {
            throw new ArgumentException("Selected bindings cannot contain null values.", nameof(bindings));
        }

        IDictionary<string, string> moduleIds = new Dictionary<string, string>(StringComparer.Ordinal);
        IDictionary<string, object?> globals = new Dictionary<string, object?>(StringComparer.Ordinal);
        IDictionary<string, DotNetProviderFunction> functions =
            new Dictionary<string, DotNetProviderFunction>(StringComparer.Ordinal);

        AddHostGlobals(hostGlobals, globals);
        AddHostFunctions(hostFunctions, functions);

        foreach (DotNetStandardLibraryModuleBinding binding in selected)
        {
            ValidateModule(binding, moduleIds);
            AddGlobals(binding, globals);
            AddFunctions(binding, functions);
        }

        return new DotNetStandardLibraryComposition(
            globals.ToFrozenDictionary(StringComparer.Ordinal),
            functions.ToFrozenDictionary(StringComparer.Ordinal)
        );
    }

    private static void ValidateModule(
        DotNetStandardLibraryModuleBinding binding,
        IDictionary<string, string> moduleIds
    )
    {
        if (!string.Equals(binding.ModuleId, binding.Module.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Binding module identifier '{binding.ModuleId}' does not match declaration module identifier '{binding.Module.Id}'."
            );
        }

        if (moduleIds.TryGetValue(binding.ModuleId, out string? existing))
        {
            throw new InvalidOperationException(
                $"Duplicate module binding identifier '{binding.ModuleId}' between '{existing}' and '{binding.Module.DisplayName}'."
            );
        }

        moduleIds.Add(binding.ModuleId, binding.Module.DisplayName);

        ValidateGlobals(binding);
        ValidateFunctions(binding);
    }

    private static void ValidateGlobals(DotNetStandardLibraryModuleBinding binding)
    {
        IDictionary<string, GlobalSymbol> declarations = CreateDeclarationMap(
            binding.Module.Globals,
            static global => global.Id,
            binding,
            "global"
        );
        ISet<string> implementations = new HashSet<string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object?> implementation in binding.Globals)
        {
            if (!implementations.Add(implementation.Key))
            {
                throw new InvalidOperationException(
                    $"Module '{binding.ModuleId}' contains duplicate global implementation '{implementation.Key}'."
                );
            }

            if (!declarations.TryGetValue(implementation.Key, out GlobalSymbol? declaration))
            {
                throw new InvalidOperationException(
                    $"Module '{binding.ModuleId}' implements undeclared global '{implementation.Key}'."
                );
            }

            if (!IsCompatibleGlobalValue(declaration.Type, implementation.Value))
            {
                throw new InvalidOperationException(
                    $"Global implementation '{implementation.Key}' is incompatible with declared type '{declaration.Type}'."
                );
            }
        }

        static bool IsCompatibleGlobalValue(TypeSymbol type, object? value)
        {
            if (ReferenceEquals(type, TypeSymbols.Bool))
            {
                return value is bool;
            }

            if (ReferenceEquals(type, TypeSymbols.Int))
            {
                return value is long;
            }

            if (ReferenceEquals(type, TypeSymbols.Float))
            {
                return value is double;
            }

            if (ReferenceEquals(type, TypeSymbols.Number))
            {
                return value is long or double;
            }

            if (ReferenceEquals(type, TypeSymbols.String))
            {
                return value is string;
            }

            if (ReferenceEquals(type, TypeSymbols.Unknown))
            {
                return value is not null;
            }

            if (ReferenceEquals(type, TypeSymbols.Object))
            {
                return value is IDotNetObjectValue;
            }

            return false;
        }

        GlobalSymbol? missing = declarations.Values.FirstOrDefault(
            declaration => !implementations.Contains(declaration.Id)
        );

        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"Module '{binding.ModuleId}' is missing global implementation '{missing.Id}'."
            );
        }
    }

    private static void ValidateFunctions(DotNetStandardLibraryModuleBinding binding)
    {
        IDictionary<string, FunctionSymbol> declarations = CreateDeclarationMap(
            binding.Module.Functions,
            static function => function.Id,
            binding,
            "function"
        );
        ISet<string> implementations = new HashSet<string>(StringComparer.Ordinal);

        foreach (DotNetStandardLibraryFunction implementation in binding.Functions)
        {
            if (!implementations.Add(implementation.Id))
            {
                throw new InvalidOperationException(
                    $"Module '{binding.ModuleId}' contains duplicate function implementation '{implementation.Id}'."
                );
            }

            if (!declarations.TryGetValue(implementation.Id, out FunctionSymbol? declaration))
            {
                throw new InvalidOperationException(
                    $"Module '{binding.ModuleId}' implements undeclared function '{implementation.Id}'."
                );
            }

            if (implementation.ArgumentCount != declaration.Parameters.Count)
            {
                throw new InvalidOperationException(
                    $"Function implementation '{implementation.Id}' declares {implementation.ArgumentCount} arguments, but its declaration requires {declaration.Parameters.Count}."
                );
            }
        }

        FunctionSymbol? missing = declarations.Values.FirstOrDefault(
            declaration => !implementations.Contains(declaration.Id)
        );

        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"Module '{binding.ModuleId}' is missing function implementation '{missing.Id}'."
            );
        }
    }

    private static IDictionary<string, T> CreateDeclarationMap<T>(
        IEnumerable<T> declarations,
        Func<T, string> getId,
        DotNetStandardLibraryModuleBinding binding,
        string kind
    )
    {
        IDictionary<string, T> result = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (T declaration in declarations)
        {
            string id = getId(declaration);

            if (!result.TryAdd(id, declaration))
            {
                throw new InvalidOperationException(
                    $"Module '{binding.ModuleId}' contains duplicate {kind} declaration '{id}'."
                );
            }
        }

        return result;
    }

    private static void AddHostGlobals(
        IEnumerable<KeyValuePair<string, object?>> hostGlobals,
        IDictionary<string, object?> globals
    )
    {
        foreach (KeyValuePair<string, object?> global in hostGlobals)
        {
            if (string.IsNullOrWhiteSpace(global.Key))
            {
                throw new InvalidOperationException("A host global identifier is null or whitespace.");
            }

            if (!globals.TryAdd(global.Key, global.Value))
            {
                throw new InvalidOperationException($"Duplicate host global implementation '{global.Key}'.");
            }
        }
    }

    private static void AddHostFunctions(
        IEnumerable<KeyValuePair<string, DotNetProviderFunction>> hostFunctions,
        IDictionary<string, DotNetProviderFunction> functions
    )
    {
        foreach (KeyValuePair<string, DotNetProviderFunction> function in hostFunctions)
        {
            if (string.IsNullOrWhiteSpace(function.Key) || function.Value is null)
            {
                throw new InvalidOperationException("A host function implementation is invalid.");
            }

            if (!functions.TryAdd(function.Key, function.Value))
            {
                throw new InvalidOperationException($"Duplicate host function implementation '{function.Key}'.");
            }
        }
    }

    private static void AddGlobals(
        DotNetStandardLibraryModuleBinding binding,
        IDictionary<string, object?> globals
    )
    {
        foreach (KeyValuePair<string, object?> global in binding.Globals)
        {
            if (!globals.TryAdd(global.Key, global.Value))
            {
                throw new InvalidOperationException(
                    $"Global implementation collision for '{global.Key}' while adding module '{binding.ModuleId}'."
                );
            }
        }
    }

    private static void AddFunctions(
        DotNetStandardLibraryModuleBinding binding,
        IDictionary<string, DotNetProviderFunction> functions
    )
    {
        foreach (DotNetStandardLibraryFunction function in binding.Functions)
        {
            if (!functions.TryAdd(function.Id, function.Implementation))
            {
                throw new InvalidOperationException(
                    $"Function implementation collision for '{function.Id}' while adding module '{binding.ModuleId}'."
                );
            }
        }
    }
}
