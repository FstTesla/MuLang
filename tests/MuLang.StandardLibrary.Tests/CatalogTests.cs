using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Collections;

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
    public void ExposesCompleteCanonicalCatalogInDeterministicOrder()
    {
        Assert.That(
            StandardLibraryCatalog.All.Select(static module => module.Id),
            Is.EqualTo(ExpectedModuleIds)
        );
        Assert.That(
            StandardLibraryCatalog.All.Select(static module => module.DisplayName),
            Is.EqualTo(
                [
                    "Math.Constants",
                    "Math.Basic",
                    "Math.Rounding",
                    "Math.Powers",
                    "Math.Trigonometry",
                    "Math.Classification",
                    "Array",
                    "Object",
                    "String.Inspection",
                    "String.Search",
                    "String.Transform",
                    "String.Slicing",
                    "String.Replacement",
                    "String.Comparison",
                    "Parsing",
                    "Random",
                    "Clock",
                    "Guid",
                    "Text.Encoding",
                ]
            )
        );
    }

    [Test]
    public void UsesCanonicalInstances()
    {
        StandardLibraryModule[] properties =
        [
            StandardLibraryCatalog.MathConstants,
            StandardLibraryCatalog.MathBasic,
            StandardLibraryCatalog.MathRounding,
            StandardLibraryCatalog.MathPowers,
            StandardLibraryCatalog.MathTrigonometry,
            StandardLibraryCatalog.MathClassification,
            StandardLibraryCatalog.Array,
            StandardLibraryCatalog.Object,
            StandardLibraryCatalog.StringInspection,
            StandardLibraryCatalog.StringSearch,
            StandardLibraryCatalog.StringTransform,
            StandardLibraryCatalog.StringSlicing,
            StandardLibraryCatalog.StringReplacement,
            StandardLibraryCatalog.StringComparison,
            StandardLibraryCatalog.Parsing,
            StandardLibraryCatalog.Random,
            StandardLibraryCatalog.Clock,
            StandardLibraryCatalog.Guid,
            StandardLibraryCatalog.TextEncoding,
        ];

        for (int index = 0; index < properties.Length; index++)
        {
            Assert.That(StandardLibraryCatalog.All[index], Is.SameAs(properties[index]));
        }
    }

    [Test]
    public void DeclaresMinimumVersionsAndCapabilities()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                StandardLibraryCatalog.Array.MinimumLanguageVersion,
                Is.EqualTo(LanguageVersion.Version1_1)
            );
            Assert.That(
                StandardLibraryCatalog.Object.MinimumLanguageVersion,
                Is.EqualTo(LanguageVersion.Version1_1)
            );
            Assert.That(
                StandardLibraryCatalog.All
                    .Except([ StandardLibraryCatalog.Array, StandardLibraryCatalog.Object ])
                    .Select(static module => module.MinimumLanguageVersion),
                Is.All.EqualTo(LanguageVersion.Version1)
            );
            Assert.That(
                StandardLibraryCatalog.Random.Capability,
                Is.EqualTo(StandardLibraryCapability.Randomness)
            );
            Assert.That(
                StandardLibraryCatalog.Clock.Capability,
                Is.EqualTo(StandardLibraryCapability.Clock)
            );
            Assert.That(
                StandardLibraryCatalog.Guid.Capability,
                Is.EqualTo(StandardLibraryCapability.RandomnessAndClock)
            );
            Assert.That(
                StandardLibraryCatalog.All
                    .Except(
                        [
                            StandardLibraryCatalog.Random,
                            StandardLibraryCatalog.Clock,
                            StandardLibraryCatalog.Guid,
                        ]
                    )
                    .Select(static module => module.Capability),
                Is.All.EqualTo(StandardLibraryCapability.Deterministic)
            );
        }
    }

    [Test]
    public void CatalogDeclarationsAreGloballyUnique()
    {
        IReadOnlyList<GlobalSymbol> globals =
        [
            .. StandardLibraryCatalog.All.SelectMany(static module => module.Globals),
        ];
        IReadOnlyList<FunctionSymbol> functions =
        [
            .. StandardLibraryCatalog.All.SelectMany(static module => module.Functions),
        ];
        IReadOnlyList<string> providerIds =
        [
            .. StandardLibraryCatalog.All.SelectMany(
                static module => module.Types.Select(static type => type.Id!)
                    .Concat(module.Globals.Select(static global => global.Id))
                    .Concat(module.Functions.Select(static function => function.Id))
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StandardLibraryCatalog.All.SelectMany(static module => module.Types), Is.Empty);
            Assert.That(StandardLibraryCatalog.All.Sum(static module => module.Globals.Count), Is.EqualTo(5));
            Assert.That(StandardLibraryCatalog.All.Sum(static module => module.Functions.Count), Is.EqualTo(66));
            Assert.That(
                StandardLibraryCatalog.All.Select(static module => module.Id),
                Is.Unique
            );
            Assert.That(globals.Select(static global => global.Name), Is.Unique);
            Assert.That(functions.Select(static function => function.Name), Is.Unique);
            Assert.That(providerIds, Is.Unique);
        }
    }

    [Test]
    public void ModuleCollectionsAreReadOnlySnapshots()
    {
        List<GlobalSymbol> globals =
        [
            new ("mulang.std.test.global.value", "value", TypeSymbols.Int),
        ];
        StandardLibraryModule module = new (
            "mulang.std.test",
            "Test",
            LanguageVersion.Version1_1,
            StandardLibraryCapability.Deterministic,
            [ ],
            globals,
            [ ]
        );

        globals.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(module.Globals, Has.Count.EqualTo(1));
            Assert.That(module.Globals, Is.AssignableTo<IList>());
            Assert.That(
                () => ((IList)module.Globals).Add(
                    new GlobalSymbol("mulang.std.test.global.other", "other", TypeSymbols.Int)
                ),
                Throws.InstanceOf<NotSupportedException>()
            );
        }
    }
}
