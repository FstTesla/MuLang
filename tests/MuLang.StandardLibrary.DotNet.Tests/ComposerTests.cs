using MuLang.Exporters.DotNet;
using MuLang.StandardLibrary;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class ComposerTests
{
    private static readonly DotNetProviderFunction NoOp = static (_, _) => null;

    [Test]
    public void ComposesWithHostImplementations()
    {
        DotNetStandardLibraryComposition composition = DotNetStandardLibraryComposer.Compose(
            [ DotNetStandardLibraryModules.MathBasic ],
            [ KeyValuePair.Create<string, object?>("host.global", 1L) ],
            [ KeyValuePair.Create("host.function", NoOp) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composition.Globals["host.global"], Is.EqualTo(1L));
            Assert.That(composition.Functions["host.function"], Is.SameAs(NoOp));
        }
    }

    [Test]
    public void RejectsModuleIdentifierMismatch()
    {
        DotNetStandardLibraryModuleBinding binding = new (
            "wrong",
            StandardLibraryCatalog.Array,
            [ ],
            DotNetStandardLibraryModules.Array.Functions
        );

        Assert.That(
            () => DotNetStandardLibraryComposer.Compose([ binding ]),
            Throws.InvalidOperationException.With.Message.Contains("does not match")
        );
    }

    [Test]
    public void RejectsMissingExtraDuplicateAndWrongArityImplementations()
    {
        DotNetStandardLibraryModuleBinding canonical = DotNetStandardLibraryModules.Array;
        string id = canonical.Module.Functions[0].Id;

        Assert.Multiple(() =>
        {
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [ new DotNetStandardLibraryModuleBinding(canonical.ModuleId, canonical.Module, [ ], [ ]) ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("missing function")
            );
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [
                        new DotNetStandardLibraryModuleBinding(
                            canonical.ModuleId,
                            canonical.Module,
                            [ ],
                            [ .. canonical.Functions, new DotNetStandardLibraryFunction("extra", 0, NoOp) ]
                        ),
                    ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("undeclared function")
            );
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [
                        new DotNetStandardLibraryModuleBinding(
                            canonical.ModuleId,
                            canonical.Module,
                            [ ],
                            [ .. canonical.Functions, canonical.Functions[0] ]
                        ),
                    ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("duplicate function")
            );
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [
                        new DotNetStandardLibraryModuleBinding(
                            canonical.ModuleId,
                            canonical.Module,
                            [ ],
                            [ new DotNetStandardLibraryFunction(id, 1, NoOp) ]
                        ),
                    ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("requires 2")
            );
        });
    }

    [Test]
    public void RejectsMissingExtraAndDuplicateGlobals()
    {
        DotNetStandardLibraryModuleBinding canonical = DotNetStandardLibraryModules.MathConstants;

        Assert.Multiple(() =>
        {
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [ new DotNetStandardLibraryModuleBinding(canonical.ModuleId, canonical.Module, [ ], [ ]) ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("missing global")
            );
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [
                        new DotNetStandardLibraryModuleBinding(
                            canonical.ModuleId,
                            canonical.Module,
                            [ .. canonical.Globals, KeyValuePair.Create<string, object?>("extra", null) ],
                            [ ]
                        ),
                    ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("undeclared global")
            );
            Assert.That(
                () => DotNetStandardLibraryComposer.Compose(
                    [
                        new DotNetStandardLibraryModuleBinding(
                            canonical.ModuleId,
                            canonical.Module,
                            [ .. canonical.Globals, canonical.Globals[0] ],
                            [ ]
                        ),
                    ]
                ),
                Throws.InvalidOperationException.With.Message.Contains("duplicate global")
            );
        });
    }

    [Test]
    public void RejectsGlobalValuesIncompatibleWithDeclarations()
    {
        DotNetStandardLibraryModuleBinding canonical = DotNetStandardLibraryModules.MathConstants;
        KeyValuePair<string, object?>[] globals = canonical.Globals
            .Select(
                (global, index) => index == 0
                    ? KeyValuePair.Create<string, object?>(global.Key, "invalid")
                    : global
            )
            .ToArray();
        DotNetStandardLibraryModuleBinding binding = new (
            canonical.ModuleId,
            canonical.Module,
            globals,
            canonical.Functions
        );

        Assert.That(
            () => DotNetStandardLibraryComposer.Compose([ binding ]),
            Throws.InvalidOperationException
                .With.Message.Contains(canonical.Globals[0].Key)
                .And.Message.Contains("incompatible")
        );
    }

    [Test]
    public void RejectsHostCollisions()
    {
        DotNetStandardLibraryModuleBinding binding = DotNetStandardLibraryModules.MathBasic;
        string functionId = binding.Functions[0].Id;

        Assert.That(
            () => DotNetStandardLibraryComposer.Compose(
                [ binding ],
                [ ],
                [ KeyValuePair.Create(functionId, NoOp) ]
            ),
            Throws.InvalidOperationException.With.Message.Contains("collision").And.Message.Contains(functionId)
        );
    }
}
