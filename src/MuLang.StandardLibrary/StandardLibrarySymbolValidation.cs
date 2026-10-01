using MuLang.Core;
using System.Collections.ObjectModel;

namespace MuLang.StandardLibrary;

internal static class StandardLibrarySymbolValidation
{
    public static void ValidateMetadata(
        string id,
        string name,
        LanguageVersion minimumLanguageVersion,
        StandardLibraryCapability capability
    )
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Symbol identifier cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Symbol name cannot be null or whitespace.", nameof(name));
        }

        if (!Enum.IsDefined(minimumLanguageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLanguageVersion));
        }

        if ((capability & ~StandardLibraryCapability.RandomnessAndClock) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capability));
        }
    }

    public static IReadOnlyList<IStandardLibrarySymbol> CopyDependencies(
        IEnumerable<IStandardLibrarySymbol>? dependencies,
        string symbolId
    )
    {
        if (dependencies is null)
        {
            return [ ];
        }

        IStandardLibrarySymbol[] copy = [ .. dependencies ];

        if (copy.Any(static dependency => dependency is null))
        {
            throw new ArgumentException(
                "Symbol dependencies cannot contain null values.",
                nameof(dependencies)
            );
        }

        if (copy.Any(dependency => dependency.Id == symbolId))
        {
            throw new ArgumentException(
                $"Symbol '{symbolId}' cannot depend directly on itself.",
                nameof(dependencies)
            );
        }

        IGrouping<string, IStandardLibrarySymbol>? duplicate = copy
            .GroupBy(static dependency => dependency.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Dependency identifier '{duplicate.Key}' is specified more than once.",
                nameof(dependencies)
            );
        }

        return new ReadOnlyCollection<IStandardLibrarySymbol>(copy);
    }
}
