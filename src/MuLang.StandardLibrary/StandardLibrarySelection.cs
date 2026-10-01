using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Collections.ObjectModel;

namespace MuLang.StandardLibrary;

/// <summary>Represents a normalized selection of standard-library symbols.</summary>
public sealed class StandardLibrarySelection
{
    private StandardLibrarySelection(IReadOnlyList<IStandardLibrarySymbol> symbols)
    {
        Symbols = symbols;
        Types = new ReadOnlyCollection<StandardLibraryType>(
            [ .. symbols.OfType<StandardLibraryType>() ]
        );
        Globals = new ReadOnlyCollection<StandardLibraryGlobal>(
            [ .. symbols.OfType<StandardLibraryGlobal>() ]
        );
        Functions = new ReadOnlyCollection<StandardLibraryFunction>(
            [ .. symbols.OfType<StandardLibraryFunction>() ]
        );
        MinimumLanguageVersion = symbols.Count == 0
            ? LanguageVersion.Version1
            : symbols.Max(static symbol => symbol.MinimumLanguageVersion);
        Capability = symbols.Aggregate(
            StandardLibraryCapability.Deterministic,
            static (capability, symbol) => capability | symbol.Capability
        );
    }

    /// <summary>Gets the selected symbols in dependencies-first order.</summary>
    public IReadOnlyList<IStandardLibrarySymbol> Symbols { get; }

    /// <summary>Gets the selected structured types in dependencies-first order.</summary>
    public IReadOnlyList<StandardLibraryType> Types { get; }

    /// <summary>Gets the selected globals in dependencies-first order.</summary>
    public IReadOnlyList<StandardLibraryGlobal> Globals { get; }

    /// <summary>Gets the selected functions in dependencies-first order.</summary>
    public IReadOnlyList<StandardLibraryFunction> Functions { get; }

    /// <summary>Gets the minimum supported language version.</summary>
    public LanguageVersion MinimumLanguageVersion { get; }

    /// <summary>Gets the required runtime capabilities.</summary>
    public StandardLibraryCapability Capability { get; }

    /// <summary>Creates a normalized selection from symbols.</summary>
    /// <param name="symbols">The explicitly selected symbols.</param>
    /// <returns>The normalized selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="symbols" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a symbol or its metadata is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when identifiers conflict or dependencies contain a cycle.</exception>
    public static StandardLibrarySelection Create(
        IEnumerable<IStandardLibrarySymbol> symbols
    )
    {
        return Create(symbols, [ ]);
    }

    /// <summary>Creates a normalized selection from modules.</summary>
    /// <param name="modules">The selected modules.</param>
    /// <returns>The normalized selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modules" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a module or symbol is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when identifiers conflict or dependencies contain a cycle.</exception>
    public static StandardLibrarySelection CreateFromModules(
        IEnumerable<StandardLibraryModule> modules
    )
    {
        return Create([ ], modules);
    }

    /// <summary>Creates a normalized selection from symbols and modules.</summary>
    /// <param name="symbols">The explicitly selected symbols.</param>
    /// <param name="modules">The selected modules.</param>
    /// <returns>The normalized selection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a module or symbol is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when identifiers conflict or dependencies contain a cycle.</exception>
    public static StandardLibrarySelection Create(
        IEnumerable<IStandardLibrarySymbol> symbols,
        IEnumerable<StandardLibraryModule> modules
    )
    {
        if (symbols is null)
        {
            throw new ArgumentNullException(nameof(symbols));
        }

        if (modules is null)
        {
            throw new ArgumentNullException(nameof(modules));
        }

        IStandardLibrarySymbol[] explicitSymbols = [ .. symbols ];
        StandardLibraryModule[] selectedModules = [ .. modules ];

        if (explicitSymbols.Any(static symbol => symbol is null))
        {
            throw new ArgumentException(
                "Selected symbols cannot contain null values.",
                nameof(symbols)
            );
        }

        if (selectedModules.Any(static module => module is null))
        {
            throw new ArgumentException(
                "Selected modules cannot contain null values.",
                nameof(modules)
            );
        }

        IReadOnlyList<IStandardLibrarySymbol> roots =
        [
            .. explicitSymbols,
            .. selectedModules.SelectMany(static module => module.Symbols),
        ];
        IDictionary<string, IStandardLibrarySymbol> symbolsById =
            new Dictionary<string, IStandardLibrarySymbol>(StringComparer.Ordinal);
        IDictionary<string, VisitState> states =
            new Dictionary<string, VisitState>(StringComparer.Ordinal);
        IList<IStandardLibrarySymbol> ordered = new List<IStandardLibrarySymbol>();
        IList<string> path = new List<string>();

        foreach (IStandardLibrarySymbol symbol in roots)
        {
            Visit(symbol);
        }

        return new StandardLibrarySelection(
            new ReadOnlyCollection<IStandardLibrarySymbol>([ .. ordered ])
        );

        void Visit(IStandardLibrarySymbol symbol)
        {
            ValidateSymbol(symbol);

            if (symbolsById.TryGetValue(symbol.Id, out IStandardLibrarySymbol? existing))
            {
                if (!AreEquivalent(existing, symbol))
                {
                    throw new InvalidOperationException(
                        $"Conflicting standard-library symbol identifier '{symbol.Id}'."
                    );
                }

                symbol = existing;
            }
            else
            {
                symbolsById.Add(symbol.Id, symbol);
            }

            if (states.TryGetValue(symbol.Id, out VisitState state))
            {
                if (state == VisitState.Visited)
                {
                    return;
                }

                int cycleStart = path.IndexOf(symbol.Id);
                IEnumerable<string> cycle = path.Skip(cycleStart).Append(symbol.Id);
                throw new InvalidOperationException(
                    $"Standard-library symbol dependency cycle detected: {string.Join(" -> ", cycle)}."
                );
            }

            states.Add(symbol.Id, VisitState.Visiting);
            path.Add(symbol.Id);

            foreach (IStandardLibrarySymbol dependency in symbol.Dependencies)
            {
                Visit(dependency);
            }

            path.RemoveAt(path.Count - 1);
            states[symbol.Id] = VisitState.Visited;
            ordered.Add(symbol);
        }
    }

    private static void ValidateSymbol(IStandardLibrarySymbol symbol)
    {
        if (symbol is null)
        {
            throw new ArgumentException("Symbol dependencies cannot contain null values.");
        }

        StandardLibrarySymbolValidation.ValidateMetadata(
            symbol.Id,
            symbol.Name,
            symbol.MinimumLanguageVersion,
            symbol.Capability
        );

        if (symbol.Dependencies is null)
        {
            throw new ArgumentException(
                $"Symbol '{symbol.Id}' has a null dependency collection."
            );
        }
    }

    private static bool AreEquivalent(
        IStandardLibrarySymbol left,
        IStandardLibrarySymbol right
    )
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (
            left.GetType() != right.GetType() ||
            left.Name != right.Name ||
            left.MinimumLanguageVersion != right.MinimumLanguageVersion ||
            left.Capability != right.Capability ||
            !left.Dependencies.Select(static dependency => dependency.Id).SequenceEqual(
                right.Dependencies.Select(static dependency => dependency.Id),
                StringComparer.Ordinal
            )
        )
        {
            return false;
        }

        return (left, right) switch
        {
            (StandardLibraryType first, StandardLibraryType second) =>
                AreEquivalent(first.Declaration, second.Declaration),
            (StandardLibraryGlobal first, StandardLibraryGlobal second) =>
                AreEquivalent(first.Declaration, second.Declaration),
            (StandardLibraryFunction first, StandardLibraryFunction second) =>
                AreEquivalent(first.Declaration, second.Declaration),
            _ => true,
        };
    }

    private static bool AreEquivalent(ObjectTypeSymbol left, ObjectTypeSymbol right)
    {
        return
            left.Id == right.Id &&
            left.Name == right.Name &&
            left.IsOpen == right.IsOpen &&
            left.Properties.Select(Format).SequenceEqual(right.Properties.Select(Format));

        static string Format(ObjectPropertySymbol property)
        {
            return $"{property.Name}:{property.Type.DisplayName}:{property.IsOptional}";
        }
    }

    private static bool AreEquivalent(GlobalSymbol left, GlobalSymbol right)
    {
        return
            left.Id == right.Id &&
            left.Name == right.Name &&
            left.Type.DisplayName == right.Type.DisplayName;
    }

    private static bool AreEquivalent(FunctionSymbol left, FunctionSymbol right)
    {
        return
            left.Id == right.Id &&
            left.Name == right.Name &&
            left.ReturnType.DisplayName == right.ReturnType.DisplayName &&
            left.Parameters.Select(Format).SequenceEqual(right.Parameters.Select(Format));

        static string Format(ParameterSymbol parameter)
        {
            return $"{parameter.Name}:{parameter.Type.DisplayName}";
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }
}
