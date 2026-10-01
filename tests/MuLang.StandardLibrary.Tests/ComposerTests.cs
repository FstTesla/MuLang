using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary.Tests;

public sealed class ComposerTests
{
    [Test]
    public void ComposesOnlySelectedModules()
    {
        EnvironmentSchema schema = StandardLibraryComposer.Compose(
            [ StandardLibraryCatalog.MathConstants, StandardLibraryCatalog.Clock ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.Globals.Select(static global => global.Name), Is.EquivalentTo([ "e", "pi", "tau", "minInt", "maxInt" ]));
            Assert.That(schema.Functions.Select(static function => function.Name), Is.EquivalentTo([ "unixTimeSeconds", "unixTimeMilliseconds" ]));
            Assert.That(schema.TryGetFunction("abs", out _), Is.False);
        }
    }

    [Test]
    public void ProducesOrderIndependentFingerprint()
    {
        EnvironmentSchema first = StandardLibraryComposer.Compose(
            [ StandardLibraryCatalog.MathBasic, StandardLibraryCatalog.StringSearch ]
        );
        EnvironmentSchema second = StandardLibraryComposer.Compose(
            [ StandardLibraryCatalog.StringSearch, StandardLibraryCatalog.MathBasic ]
        );

        Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
    }

    [Test]
    public void ComposesCompleteCatalog()
    {
        EnvironmentSchema schema = StandardLibraryComposer.Compose(StandardLibraryCatalog.All);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.Globals, Has.Count.EqualTo(5));
            Assert.That(schema.Functions, Has.Count.EqualTo(66));
        }
    }

    [Test]
    public void RejectsIncompatibleLanguageVersion()
    {
        Assert.That(
            () => StandardLibraryComposer.Compose(
                [ StandardLibraryCatalog.Array ],
                LanguageVersion.Version1
            ),
            Throws.InvalidOperationException
                .With.Message.Contains(StandardLibraryCatalog.Array.Id)
                .And.Message.Contains(nameof(LanguageVersion.Version1_1))
                .And.Message.Contains(nameof(LanguageVersion.Version1))
        );
    }

    [Test]
    public void ComposesVersion1CompatibleModulesForVersion1()
    {
        EnvironmentSchema schema = StandardLibraryComposer.Compose(
            [ StandardLibraryCatalog.MathBasic, StandardLibraryCatalog.StringSearch ],
            LanguageVersion.Version1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.LanguageVersion, Is.EqualTo(LanguageVersion.Version1));
            Assert.That(schema.TryGetFunction("abs", out _), Is.True);
            Assert.That(schema.TryGetFunction("indexOf", out _), Is.True);
        }
    }

    [Test]
    public void RejectsDuplicateModuleIdentifiers()
    {
        StandardLibraryModule duplicate = CreateModule(
            StandardLibraryCatalog.MathBasic.Id,
            "Duplicate"
        );

        Assert.That(
            () => StandardLibraryComposer.Compose(
                [ StandardLibraryCatalog.MathBasic, duplicate ]
            ),
            Throws.InvalidOperationException
                .With.Message.Contains("Duplicate module identifier")
                .And.Message.Contains(StandardLibraryCatalog.MathBasic.Id)
                .And.Message.Contains("Math.Basic")
                .And.Message.Contains("Duplicate")
        );
    }

    [TestCaseSource(nameof(ModuleCollisionCases))]
    public void RejectsDeclarationCollisions(
        StandardLibraryModule first,
        StandardLibraryModule second,
        string expectedKind,
        string expectedValue
    )
    {
        Assert.That(
            () => StandardLibraryComposer.Compose([ first, second ]),
            Throws.InvalidOperationException
                .With.Message.Contains(expectedKind)
                .And.Message.Contains(expectedValue)
                .And.Message.Contains(first.Id)
                .And.Message.Contains(second.Id)
        );
    }

    [Test]
    public void RejectsProviderIdentifierCollisionsAcrossDeclarationKinds()
    {
        StandardLibraryModule globalModule = CreateModule(
            "mulang.std.first",
            "First",
            globals:
            [
                new GlobalSymbol("shared.provider", "firstGlobal", TypeSymbols.Int),
            ]
        );
        StandardLibraryModule functionModule = CreateModule(
            "mulang.std.second",
            "Second",
            functions:
            [
                new FunctionSymbol("shared.provider", "secondFunction", [ ], TypeSymbols.Int),
            ]
        );

        Assert.That(
            () => StandardLibraryComposer.Compose([ globalModule, functionModule ]),
            Throws.InvalidOperationException
                .With.Message.Contains("provider identifier")
                .And.Message.Contains("shared.provider")
                .And.Message.Contains(globalModule.Id)
                .And.Message.Contains(functionModule.Id)
        );
    }

    [Test]
    public void ReportsHostNameCollisionsPrecisely()
    {
        EnvironmentSchema host = new EnvironmentBuilder()
            .AddFunction("host.abs", "abs", [ ], TypeSymbols.Int)
            .Build();

        Assert.That(
            () => StandardLibraryComposer.Compose(
                host,
                [ StandardLibraryCatalog.MathBasic ]
            ),
            Throws.InvalidOperationException
                .With.Message.Contains("function name")
                .And.Message.Contains("host function 'abs'")
                .And.Message.Contains(StandardLibraryCatalog.MathBasic.Id)
        );
    }

    [Test]
    public void ReportsHostProviderIdentifierCollisionsPrecisely()
    {
        string providerId = StandardLibraryCatalog.MathConstants.Globals[0].Id;
        EnvironmentSchema host = new EnvironmentBuilder()
            .AddFunction(providerId, "hostFunction", [ ], TypeSymbols.Int)
            .Build();

        Assert.That(
            () => StandardLibraryComposer.Compose(
                host,
                [ StandardLibraryCatalog.MathConstants ]
            ),
            Throws.InvalidOperationException
                .With.Message.Contains("provider identifier")
                .And.Message.Contains("host function 'hostFunction'")
                .And.Message.Contains("global 'e'")
        );
    }

    [Test]
    public void PreservesHostDeclarations()
    {
        EnvironmentSchema host = new EnvironmentBuilder()
            .AddGlobal("host.value", "hostValue", TypeSymbols.String)
            .Build();

        EnvironmentSchema composed = StandardLibraryComposer.Compose(
            host,
            [ StandardLibraryCatalog.Parsing ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composed.TryGetGlobal("hostValue", out _), Is.True);
            Assert.That(composed.TryGetFunction("parseInt", out _), Is.True);
        }
    }

    private static IEnumerable<TestCaseData> ModuleCollisionCases()
    {
        yield return new TestCaseData(
            CreateModule(
                "mulang.std.first",
                "First",
                types:
                [
                    new ObjectTypeSymbol("first.type", "Shared", false, [ ]),
                ]
            ),
            CreateModule(
                "mulang.std.second",
                "Second",
                types:
                [
                    new ObjectTypeSymbol("second.type", "Shared", false, [ ]),
                ]
            ),
            "type name",
            "Shared"
        );
        yield return new TestCaseData(
            CreateModule(
                "mulang.std.first",
                "First",
                globals:
                [
                    new GlobalSymbol("first.global", "shared", TypeSymbols.Int),
                ]
            ),
            CreateModule(
                "mulang.std.second",
                "Second",
                globals:
                [
                    new GlobalSymbol("second.global", "shared", TypeSymbols.Int),
                ]
            ),
            "global name",
            "shared"
        );
        yield return new TestCaseData(
            CreateModule(
                "mulang.std.first",
                "First",
                functions:
                [
                    new FunctionSymbol("first.function", "shared", [ ], TypeSymbols.Int),
                ]
            ),
            CreateModule(
                "mulang.std.second",
                "Second",
                functions:
                [
                    new FunctionSymbol("second.function", "shared", [ ], TypeSymbols.Int),
                ]
            ),
            "function name",
            "shared"
        );
    }

    private static StandardLibraryModule CreateModule(
        string id,
        string displayName,
        IEnumerable<ObjectTypeSymbol>? types = null,
        IEnumerable<GlobalSymbol>? globals = null,
        IEnumerable<FunctionSymbol>? functions = null
    )
    {
        return new StandardLibraryModule(
            id,
            displayName,
            LanguageVersion.Version1_1,
            StandardLibraryCapability.Deterministic,
            types ?? [ ],
            globals ?? [ ],
            functions ?? [ ]
        );
    }
}
