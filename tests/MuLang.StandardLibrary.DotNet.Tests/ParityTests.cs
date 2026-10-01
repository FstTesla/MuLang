using MuLang.Core.Types;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class ParityTests
{
    [Test]
    public void CompleteCatalogHasExactImplementationParity()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            StandardLibraryCatalog.Symbols
        );
        DotNetStandardLibraryBindings bindings = DotNetStandardLibrary.Default.Bind(selection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                bindings.Globals.Keys,
                Is.EquivalentTo(StandardLibraryCatalog.Globals.All.Select(static global => global.Id))
            );
            Assert.That(
                bindings.Functions.Keys,
                Is.EquivalentTo(StandardLibraryCatalog.Functions.All.Select(static function => function.Id))
            );
            Assert.That(bindings.Globals, Has.Count.EqualTo(StandardLibraryCatalog.Globals.All.Count));
            Assert.That(bindings.Functions, Has.Count.EqualTo(StandardLibraryCatalog.Functions.All.Count));
        }

        foreach (StandardLibraryGlobal global in StandardLibraryCatalog.Globals.All)
        {
            Assert.That(
                IsCompatible(global.Declaration.Type, bindings.Globals[global.Id]),
                Is.True,
                global.Id
            );
        }
    }

    [Test]
    public void DefaultIsSingleton()
    {
        Assert.That(DotNetStandardLibrary.Default, Is.SameAs(DotNetStandardLibrary.Default));
    }

    private static bool IsCompatible(TypeSymbol type, object? value)
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

        return false;
    }
}
