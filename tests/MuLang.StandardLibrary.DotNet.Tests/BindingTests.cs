using MuLang.Core.Symbols;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class BindingTests
{
    [Test]
    public void BindsSingleGlobalOrFunction()
    {
        DotNetStandardLibraryBindings globalBindings = DotNetStandardLibrary.Default.Bind(
            StandardLibrarySelection.Create([ StandardLibraryCatalog.Globals.Pi ])
        );
        DotNetStandardLibraryBindings functionBindings = DotNetStandardLibrary.Default.Bind(
            StandardLibrarySelection.Create([ StandardLibraryCatalog.Functions.Abs ])
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(globalBindings.Globals, Has.Count.EqualTo(1));
            Assert.That(globalBindings.Globals[StandardLibraryCatalog.Globals.Pi.Id], Is.EqualTo(Math.PI));
            Assert.That(globalBindings.Functions, Is.Empty);
            Assert.That(functionBindings.Globals, Is.Empty);
            Assert.That(functionBindings.Functions.Keys, Is.EqualTo([ StandardLibraryCatalog.Functions.Abs.Id ]));
        }
    }

    [Test]
    public void BindsModuleSelection()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.CreateFromModules(
            [ StandardLibraryCatalog.Modules.MathConstants ]
        );
        DotNetStandardLibraryBindings bindings = DotNetStandardLibrary.Default.Bind(selection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bindings.Globals.Keys, Is.EquivalentTo(StandardLibraryCatalog.Modules.MathConstants.Globals.Select(static global => global.Id)));
            Assert.That(bindings.Functions, Is.Empty);
        }
    }

    [Test]
    public void BindsMixedSelectionWithoutDuplicates()
    {
        StandardLibrarySelection selection = StandardLibrarySelection.Create(
            [
                StandardLibraryCatalog.Functions.Abs,
                StandardLibraryCatalog.Functions.Abs,
                StandardLibraryCatalog.Globals.Pi,
            ],
            [ StandardLibraryCatalog.Modules.MathBasic ]
        );
        DotNetStandardLibraryBindings bindings = DotNetStandardLibrary.Default.Bind(selection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bindings.Globals.Keys, Is.EqualTo([ StandardLibraryCatalog.Globals.Pi.Id ]));
            Assert.That(bindings.Functions, Has.Count.EqualTo(StandardLibraryCatalog.Modules.MathBasic.Functions.Count));
            Assert.That(bindings.Functions.Keys, Is.Unique);
        }
    }

    [Test]
    public void RejectsUnsupportedAndNoncanonicalSymbols()
    {
        StandardLibraryFunction canonical = StandardLibraryCatalog.Functions.Abs;
        StandardLibraryFunction noncanonical = new (
            canonical.Declaration,
            canonical.MinimumLanguageVersion,
            canonical.Capability,
            canonical.Dependencies
        );
        StandardLibraryFunction unsupported = new (
            new FunctionSymbol(
                "mulang.std.unsupported.function.value",
                "value",
                [ ],
                TypeSymbols.Int
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    () => DotNetStandardLibrary.Default.Bind(
                        StandardLibrarySelection.Create([ noncanonical ])
                    ),
                    Throws.ArgumentException.With.Message.Contains(canonical.Id).And.Message.Contains("canonical")
                );
                Assert.That(
                    () => DotNetStandardLibrary.Default.Bind(
                        StandardLibrarySelection.Create([ unsupported ])
                    ),
                    Throws.ArgumentException.With.Message.Contains(unsupported.Id).And.Message.Contains("not supported")
                );
            }
        );
    }

    [Test]
    public void BindingsAreImmutable()
    {
        DotNetStandardLibraryBindings bindings = DotNetStandardLibrary.Default.Bind(
            StandardLibrarySelection.Create(
                [
                    StandardLibraryCatalog.Globals.Pi,
                    StandardLibraryCatalog.Functions.Abs,
                ]
            )
        );

        IDictionary<string, object?> globals = (IDictionary<string, object?>)bindings.Globals;
        IDictionary<string, DotNetProviderFunction> functions =
            (IDictionary<string, DotNetProviderFunction>)bindings.Functions;

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    () => globals.Add("host.global", 1L),
                    Throws.TypeOf<NotSupportedException>()
                );
                Assert.That(
                    () => functions.Remove(StandardLibraryCatalog.Functions.Abs.Id),
                    Throws.TypeOf<NotSupportedException>()
                );
            }
        );
    }

    [Test]
    public void RejectsNullSelection()
    {
        Assert.That(
            static () => DotNetStandardLibrary.Default.Bind(null!),
            Throws.ArgumentNullException.With.Property("ParamName").EqualTo("selection")
        );
    }
}
