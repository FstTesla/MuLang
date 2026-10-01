using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary.Tests;

public sealed class SelectionTests
{
    [Test]
    public void ExpandsDependenciesInDeterministicDependenciesFirstOrder()
    {
        StandardLibraryGlobal first = CreateGlobal("first");
        StandardLibraryGlobal second = CreateGlobal("second", [ first ]);
        StandardLibraryFunction third = CreateFunction("third", [ first, second ]);
        StandardLibraryFunction fourth = CreateFunction("fourth");

        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            [ third, fourth, second ]
        );

        Assert.That(
            selection.Symbols,
            Is.EqualTo(
                new IStandardLibrarySymbol[] { first, second, third, fourth }
            )
        );
    }

    [Test]
    public void DeduplicatesInstancesAndEquivalentIdentifiers()
    {
        StandardLibraryGlobal first = CreateGlobal("value");
        StandardLibraryGlobal equivalent = CreateGlobal("value");

        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            [ first, first, equivalent ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Symbols, Is.EqualTo([ first ]));
            Assert.That(selection.Globals, Is.EqualTo([ first ]));
        }
    }

    [Test]
    public void RejectsConflictingIdentifiers()
    {
        StandardLibraryGlobal first = CreateGlobal("value");
        StandardLibraryGlobal conflicting = new (
            new GlobalSymbol(first.Id, "other", TypeSymbols.Int)
        );

        Assert.That(
            () => StandardLibrarySelection.Create([ first, conflicting ]),
            Throws.InvalidOperationException
                .With.Message.Contains(first.Id)
                .And.Message.Contains("Conflicting")
        );
    }

    [Test]
    public void DetectsDependencyCycles()
    {
        MutableSymbol first = new ("mulang.std.test.global.first", "first");
        MutableSymbol second = new ("mulang.std.test.global.second", "second");
        first.Dependencies = [ second ];
        second.Dependencies = [ first ];

        Assert.That(
            () => StandardLibrarySelection.Create([ first ]),
            Throws.InvalidOperationException
                .With.Message.Contains("cycle")
                .And.Message.Contains(first.Id)
                .And.Message.Contains(second.Id)
        );
    }

    [Test]
    public void CombinesSymbolsAndModulesAndDerivesMetadata()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            [ StandardLibraryCatalog.Functions.NewGuidV7 ],
            [ StandardLibraryCatalog.Modules.Array ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                selection.Symbols,
                Is.EqualTo(
                    new IStandardLibrarySymbol[]
                    {
                        StandardLibraryCatalog.Functions.NewGuidV7,
                        StandardLibraryCatalog.Functions.ArrayContains,
                    }
                )
            );
            Assert.That(selection.MinimumLanguageVersion, Is.EqualTo(LanguageVersion.Version1_1));
            Assert.That(selection.Capability, Is.EqualTo(StandardLibraryCapability.RandomnessAndClock));
            Assert.That(selection.Types, Is.Empty);
            Assert.That(selection.Globals, Is.Empty);
            Assert.That(selection.Functions, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void EmptySelectionUsesVersionOneAndNoCapabilities()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.Create([ ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(selection.Symbols, Is.Empty);
            Assert.That(selection.MinimumLanguageVersion, Is.EqualTo(LanguageVersion.Version1));
            Assert.That(selection.Capability, Is.EqualTo(StandardLibraryCapability.Deterministic));
        }
    }

    [Test]
    public void ModuleMetadataIncludesTransitiveDependencies()
    {
        StandardLibraryGlobal dependency = new (
            new GlobalSymbol(
                "mulang.std.dependencies.global.clock",
                "clockDependency",
                TypeSymbols.Int
            ),
            LanguageVersion.Version1_1,
            StandardLibraryCapability.Clock
        );
        StandardLibraryFunction function = new (
            new FunctionSymbol(
                "mulang.std.test.function.read",
                "read",
                [ ],
                TypeSymbols.Int
            ),
            dependencies: [ dependency ]
        );
        StandardLibraryModule module = new (
            "mulang.std.test",
            "Test",
            [ function ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(module.Symbols, Is.EqualTo([ function ]));
            Assert.That(module.MinimumLanguageVersion, Is.EqualTo(LanguageVersion.Version1_1));
            Assert.That(module.Capability, Is.EqualTo(StandardLibraryCapability.Clock));
        }
    }

    private static StandardLibraryGlobal CreateGlobal(
        string name,
        IEnumerable<IStandardLibrarySymbol>? dependencies = null
    )
    {
        return new StandardLibraryGlobal(
            new GlobalSymbol($"mulang.std.test.global.{name}", name, TypeSymbols.Int),
            dependencies: dependencies
        );
    }

    private static StandardLibraryFunction CreateFunction(
        string name,
        IEnumerable<IStandardLibrarySymbol>? dependencies = null
    )
    {
        return new StandardLibraryFunction(
            new FunctionSymbol(
                $"mulang.std.test.function.{name}",
                name,
                [ ],
                TypeSymbols.Int
            ),
            dependencies: dependencies
        );
    }

    private sealed class MutableSymbol(string id, string name) : IStandardLibrarySymbol
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public LanguageVersion MinimumLanguageVersion { get; } = LanguageVersion.Version1;

        public StandardLibraryCapability Capability { get; } =
            StandardLibraryCapability.Deterministic;

        public IReadOnlyList<IStandardLibrarySymbol> Dependencies { get; set; } = [ ];
    }
}
