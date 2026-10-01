using MuLang.Exporters.DotNet;
using System.Collections.Frozen;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Provides .NET implementations for selected standard-library symbols.</summary>
public sealed class DotNetStandardLibrary
{
    private readonly DotNetStandardLibraryServices services;

    /// <summary>Initializes a new instance of the <see cref="DotNetStandardLibrary" /> class.</summary>
    /// <param name="randomOptions">The random configuration, or <c>null</c> to use the default.</param>
    /// <param name="clockOptions">The clock configuration, or <c>null</c> to use the default.</param>
    /// <param name="guidOptions">The GUID configuration, or <c>null</c> to use the default.</param>
    public DotNetStandardLibrary(
        RandomStandardLibraryOptions? randomOptions = null,
        ClockStandardLibraryOptions? clockOptions = null,
        GuidStandardLibraryOptions? guidOptions = null
    )
    {
        randomOptions ??= new RandomStandardLibraryOptions();
        clockOptions ??= new ClockStandardLibraryOptions();
        guidOptions ??= new GuidStandardLibraryOptions();

        services = new DotNetStandardLibraryServices(
            randomOptions.RandomSource ?? DotNetStandardLibraryRegistry.DefaultRandomSource,
            clockOptions.TimeProvider,
            guidOptions.TimeProvider
        );
    }

    /// <summary>Gets the shared instance using default configuration.</summary>
    public static DotNetStandardLibrary Default { get; } = new ();

    /// <summary>Creates runtime bindings for a standard-library selection.</summary>
    /// <param name="selection">The selected canonical standard-library symbols.</param>
    /// <returns>The immutable runtime bindings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a selected symbol is unsupported or noncanonical.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required selected-symbol configuration is invalid.</exception>
    public DotNetStandardLibraryBindings Bind(StandardLibrarySelection selection)
    {
        if (selection is null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        IDictionary<string, object?> globals =
            new Dictionary<string, object?>(StringComparer.Ordinal);
        IDictionary<string, DotNetProviderFunction> functions =
            new Dictionary<string, DotNetProviderFunction>(StringComparer.Ordinal);

        foreach (IStandardLibrarySymbol symbol in selection.Symbols)
        {
            switch (symbol)
            {
                case StandardLibraryType type:
                {
                    ValidateCanonical(type, StandardLibraryCatalog.Types.All);
                    break;
                }

                case StandardLibraryGlobal global:
                {
                    ValidateCanonical(global, StandardLibraryCatalog.Globals.All);

                    if (!DotNetStandardLibraryRegistry.Globals.TryGetValue(
                            global.Id,
                            out object? value
                        ))
                    {
                        throw Unsupported(global, nameof(selection));
                    }

                    globals.Add(global.Id, value);
                    break;
                }

                case StandardLibraryFunction function:
                {
                    ValidateCanonical(function, StandardLibraryCatalog.Functions.All);

                    if (!DotNetStandardLibraryRegistry.Functions.TryGetValue(
                            function.Id,
                            out Func<DotNetStandardLibraryServices, DotNetProviderFunction>? factory
                        ))
                    {
                        throw Unsupported(function, nameof(selection));
                    }

                    functions.Add(
                        function.Id,
                        factory(services)
                    );
                    break;
                }

                default:
                {
                    throw new ArgumentException(
                        $"Standard-library symbol '{symbol.Id}' has unsupported runtime kind '{symbol.GetType().Name}'.",
                        nameof(selection)
                    );
                }
            }
        }

        return new DotNetStandardLibraryBindings(
            globals.ToFrozenDictionary(StringComparer.Ordinal),
            functions.ToFrozenDictionary(StringComparer.Ordinal)
        );
    }

    private static ArgumentException Unsupported(
        IStandardLibrarySymbol symbol,
        string parameterName
    )
    {
        return new ArgumentException(
            $"Standard-library symbol '{symbol.Id}' is not supported by the .NET implementation.",
            parameterName
        );
    }

    private static void ValidateCanonical<T>(
        T symbol,
        IReadOnlyList<T> canonicalSymbols
    )
        where T : class, IStandardLibrarySymbol
    {
        T? canonical = canonicalSymbols.FirstOrDefault(
            candidate => string.Equals(candidate.Id, symbol.Id, StringComparison.Ordinal)
        );

        if (canonical is null)
        {
            throw new ArgumentException(
                $"Standard-library symbol '{symbol.Id}' is not supported by the .NET implementation.",
                "selection"
            );
        }

        if (!ReferenceEquals(symbol, canonical))
        {
            throw new ArgumentException(
                $"Standard-library symbol '{symbol.Id}' is not the canonical catalog declaration.",
                "selection"
            );
        }
    }
}
