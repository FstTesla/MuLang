using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary.Tests;

public sealed class ComposerTests
{
    [Test]
    public void ComposesSingleSymbolsAndModulesWithoutImplicitImports()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            [ StandardLibraryCatalog.Functions.Abs ],
            [ StandardLibraryCatalog.Modules.Clock ]
        );
        EnvironmentSchema schema = StandardLibraryComposer.Compose(selection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                schema.Functions.Select(static function => function.Name),
                Is.EquivalentTo([ "abs", "unixTimeSeconds", "unixTimeMilliseconds" ])
            );
            Assert.That(schema.Globals, Is.Empty);
            Assert.That(schema.TryGetFunction("sign", out _), Is.False);
        }
    }

    [Test]
    public void ProducesOrderIndependentFingerprint()
    {
        EnvironmentSchema first = StandardLibraryComposer.Compose(
            StandardLibrarySelection.Create(
                [
                    StandardLibraryCatalog.Functions.Abs,
                    StandardLibraryCatalog.Functions.IndexOf,
                ]
            )
        );
        EnvironmentSchema second = StandardLibraryComposer.Compose(
            StandardLibrarySelection.Create(
                [
                    StandardLibraryCatalog.Functions.IndexOf,
                    StandardLibraryCatalog.Functions.Abs,
                ]
            )
        );

        Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
    }

    [Test]
    public void ComposesCompleteCatalog()
    {
        EnvironmentSchema schema = StandardLibraryComposer.Compose(
            StandardLibrarySelection.CreateFromModules(StandardLibraryCatalog.Modules.All)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.Globals, Has.Count.EqualTo(5));
            Assert.That(schema.Functions, Has.Count.EqualTo(66));
        }
    }

    [Test]
    public void ReportsIncompatibleSymbolAndIdentifier()
    {
        StandardLibraryFunction symbol = StandardLibraryCatalog.Functions.ArrayContains;

        Assert.That(
            () => StandardLibraryComposer.Compose(
                StandardLibrarySelection.Create([ symbol ]),
                LanguageVersion.Version1
            ),
            Throws.InvalidOperationException
                .With.Message.Contains(symbol.Name)
                .And.Message.Contains(symbol.Id)
                .And.Message.Contains(nameof(LanguageVersion.Version1_1))
                .And.Message.Contains(nameof(LanguageVersion.Version1))
        );
    }

    [Test]
    public void PreservesHostDeclarationsAndLanguageVersion()
    {
        EnvironmentSchema host = new EnvironmentBuilder()
            .AddGlobal("host.value", "hostValue", TypeSymbols.String)
            .Build(LanguageVersion.Version1);

        EnvironmentSchema composed = StandardLibraryComposer.Compose(
            host,
            StandardLibrarySelection.Create([ StandardLibraryCatalog.Functions.ParseInt ])
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composed.LanguageVersion, Is.EqualTo(LanguageVersion.Version1));
            Assert.That(composed.TryGetGlobal("hostValue", out _), Is.True);
            Assert.That(composed.TryGetFunction("parseInt", out _), Is.True);
        }
    }

    [Test]
    public void RejectsHostAndSelectionCollisions()
    {
        StandardLibraryFunction abs = StandardLibraryCatalog.Functions.Abs;
        EnvironmentSchema nameHost = new EnvironmentBuilder()
            .AddFunction("host.abs", abs.Name, [ ], TypeSymbols.Int)
            .Build();
        EnvironmentSchema idHost = new EnvironmentBuilder()
            .AddGlobal(abs.Id, "hostValue", TypeSymbols.Int)
            .Build();
        StandardLibrarySelection selection = StandardLibrarySelection.Create([ abs ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => StandardLibraryComposer.Compose(nameHost, selection),
                Throws.InvalidOperationException
                    .With.Message.Contains("function name")
                    .And.Message.Contains("host function")
                    .And.Message.Contains(abs.Id)
            );
            Assert.That(
                () => StandardLibraryComposer.Compose(idHost, selection),
                Throws.InvalidOperationException
                    .With.Message.Contains("provider identifier")
                    .And.Message.Contains("host global")
                    .And.Message.Contains(abs.Id)
            );
        }
    }

    [TestCase("type")]
    [TestCase("global")]
    [TestCase("function")]
    public void RejectsSelectionNameCollisions(string kind)
    {
        StandardLibrarySelection selection = kind switch
        {
            "type" => StandardLibrarySelection.Create(
                [
                    CreateType("mulang.std.first.type.shared", "Shared"),
                    CreateType("mulang.std.second.type.shared", "Shared"),
                ]
            ),
            "global" => StandardLibrarySelection.Create(
                [
                    CreateGlobal("mulang.std.first.global.shared", "shared"),
                    CreateGlobal("mulang.std.second.global.shared", "shared"),
                ]
            ),
            "function" => StandardLibrarySelection.Create(
                [
                    CreateFunction("mulang.std.first.function.shared", "shared"),
                    CreateFunction("mulang.std.second.function.shared", "shared"),
                ]
            ),
            _ => throw new InvalidOperationException(),
        };

        Assert.That(
            () => StandardLibraryComposer.Compose(selection),
            Throws.InvalidOperationException
                .With.Message.Contains($"{kind} name")
                .And.Message.Contains("shared").IgnoreCase
        );
    }

    private static StandardLibraryType CreateType(string id, string name)
    {
        return new StandardLibraryType(new ObjectTypeSymbol(id, name, false, [ ]));
    }

    private static StandardLibraryGlobal CreateGlobal(string id, string name)
    {
        return new StandardLibraryGlobal(new GlobalSymbol(id, name, TypeSymbols.Int));
    }

    private static StandardLibraryFunction CreateFunction(string id, string name)
    {
        return new StandardLibraryFunction(
            new FunctionSymbol(id, name, [ ], TypeSymbols.Int)
        );
    }
}
