using MuLang.Core;
using System.Reflection;

namespace MuLang.StandardLibrary.Tests;

public sealed class CatalogTests
{
    private static readonly string[] ExpectedModuleIds =
    [
        "mulang.std.math.constants",
        "mulang.std.math.basic",
        "mulang.std.math.rounding",
        "mulang.std.math.powers",
        "mulang.std.math.trigonometry",
        "mulang.std.math.classification",
        "mulang.std.array",
        "mulang.std.object",
        "mulang.std.string.inspection",
        "mulang.std.string.search",
        "mulang.std.string.transform",
        "mulang.std.string.slicing",
        "mulang.std.string.replacement",
        "mulang.std.string.comparison",
        "mulang.std.parsing",
        "mulang.std.random",
        "mulang.std.clock",
        "mulang.std.guid",
        "mulang.std.text.encoding",
    ];

    [Test]
    public void ExposesEverySymbolPropertyAndCanonicalOrder()
    {
        PropertyInfo[] globalProperties =
        [
            .. typeof(StandardLibraryCatalog.Globals)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(static property => property.PropertyType == typeof(StandardLibraryGlobal)),
        ];
        PropertyInfo[] functionProperties =
        [
            .. typeof(StandardLibraryCatalog.Functions)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(static property => property.PropertyType == typeof(StandardLibraryFunction)),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StandardLibraryCatalog.Types.All, Is.Empty);
            Assert.That(globalProperties, Has.Length.EqualTo(5));
            Assert.That(functionProperties, Has.Length.EqualTo(66));
            Assert.That(StandardLibraryCatalog.Globals.All, Has.Count.EqualTo(5));
            Assert.That(StandardLibraryCatalog.Functions.All, Has.Count.EqualTo(66));
            Assert.That(
                StandardLibraryCatalog.Symbols,
                Is.EqualTo(
                    StandardLibraryCatalog.Globals.All
                        .Cast<IStandardLibrarySymbol>()
                        .Concat(StandardLibraryCatalog.Functions.All)
                )
            );
        }
    }

    [Test]
    public void SupportsOrdinalIdentifierAndNameLookups()
    {
        StandardLibraryGlobal global = StandardLibraryCatalog.Globals.Pi;
        StandardLibraryFunction function = StandardLibraryCatalog.Functions.ArrayContains;
        StandardLibraryModule module = StandardLibraryCatalog.Modules.MathBasic;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StandardLibraryCatalog.Globals.TryGetById(global.Id, out StandardLibraryGlobal? globalById), Is.True);
            Assert.That(globalById, Is.SameAs(global));
            Assert.That(StandardLibraryCatalog.Globals.TryGetByName(global.Name, out StandardLibraryGlobal? globalByName), Is.True);
            Assert.That(globalByName, Is.SameAs(global));
            Assert.That(StandardLibraryCatalog.Functions.TryGetById(function.Id, out StandardLibraryFunction? functionById), Is.True);
            Assert.That(functionById, Is.SameAs(function));
            Assert.That(StandardLibraryCatalog.Functions.TryGetByName(function.Name, out StandardLibraryFunction? functionByName), Is.True);
            Assert.That(functionByName, Is.SameAs(function));
            Assert.That(StandardLibraryCatalog.Modules.TryGetById(module.Id, out StandardLibraryModule? moduleById), Is.True);
            Assert.That(moduleById, Is.SameAs(module));
            Assert.That(StandardLibraryCatalog.Modules.TryGetByName(module.DisplayName, out StandardLibraryModule? moduleByName), Is.True);
            Assert.That(moduleByName, Is.SameAs(module));
            Assert.That(StandardLibraryCatalog.Globals.TryGetByName("Pi", out _), Is.False);
            Assert.That(StandardLibraryCatalog.Functions.TryGetById(function.Id.ToUpperInvariant(), out _), Is.False);
            Assert.That(StandardLibraryCatalog.Modules.TryGetByName("math.basic", out _), Is.False);
            Assert.That(StandardLibraryCatalog.Types.TryGetById("missing", out _), Is.False);
            Assert.That(StandardLibraryCatalog.Types.TryGetByName("missing", out _), Is.False);
        }
    }

    [Test]
    public void ModulesUseCanonicalSymbolInstances()
    {
        IReadOnlyList<IStandardLibrarySymbol> moduleSymbols =
        [
            .. StandardLibraryCatalog.Modules.All.SelectMany(static module => module.Symbols),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                StandardLibraryCatalog.Modules.All.Select(static module => module.Id),
                Is.EqualTo(ExpectedModuleIds)
            );
            Assert.That(moduleSymbols, Is.EqualTo(StandardLibraryCatalog.Symbols));

            foreach (IStandardLibrarySymbol symbol in moduleSymbols)
            {
                Assert.That(
                    StandardLibraryCatalog.Symbols.Any(candidate => ReferenceEquals(candidate, symbol)),
                    Is.True
                );
            }
        }
    }

    [Test]
    public void DeclaresUniqueIdentifiersAndNames()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StandardLibraryCatalog.Symbols.Select(static symbol => symbol.Id), Is.Unique);
            Assert.That(StandardLibraryCatalog.Globals.All.Select(static symbol => symbol.Name), Is.Unique);
            Assert.That(StandardLibraryCatalog.Functions.All.Select(static symbol => symbol.Name), Is.Unique);
            Assert.That(StandardLibraryCatalog.Modules.All.Select(static module => module.Id), Is.Unique);
            Assert.That(StandardLibraryCatalog.Modules.All.Select(static module => module.DisplayName), Is.Unique);
        }
    }

    [Test]
    public void AssignsMetadataPerSymbolAndDerivesModuleMetadata()
    {
        ISet<StandardLibraryFunction> versionOneOne =
            new HashSet<StandardLibraryFunction>(ReferenceEqualityComparer.Instance)
            {
                StandardLibraryCatalog.Functions.ArrayContains,
                StandardLibraryCatalog.Functions.ObjectKeys,
                StandardLibraryCatalog.Functions.ObjectValues,
            };

        foreach (StandardLibraryFunction function in StandardLibraryCatalog.Functions.All)
        {
            LanguageVersion expectedVersion = versionOneOne.Contains(function)
                ? LanguageVersion.Version1_1
                : LanguageVersion.Version1;
            StandardLibraryCapability expectedCapability = function.Name switch
            {
                "randomFloat" or "randomInt" or "newGuid" =>
                    StandardLibraryCapability.Randomness,
                "unixTimeSeconds" or "unixTimeMilliseconds" =>
                    StandardLibraryCapability.Clock,
                "newGuidV7" => StandardLibraryCapability.RandomnessAndClock,
                _ => StandardLibraryCapability.Deterministic,
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(function.MinimumLanguageVersion, Is.EqualTo(expectedVersion), function.Id);
                Assert.That(function.Capability, Is.EqualTo(expectedCapability), function.Id);
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StandardLibraryCatalog.Modules.Array.MinimumLanguageVersion, Is.EqualTo(LanguageVersion.Version1_1));
            Assert.That(StandardLibraryCatalog.Modules.Object.MinimumLanguageVersion, Is.EqualTo(LanguageVersion.Version1_1));
            Assert.That(StandardLibraryCatalog.Modules.Random.Capability, Is.EqualTo(StandardLibraryCapability.Randomness));
            Assert.That(StandardLibraryCatalog.Modules.Clock.Capability, Is.EqualTo(StandardLibraryCapability.Clock));
            Assert.That(StandardLibraryCatalog.Modules.Guid.Capability, Is.EqualTo(StandardLibraryCapability.RandomnessAndClock));
        }
    }
}
