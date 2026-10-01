using MuLang.StandardLibrary;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class ParityTests
{
    [Test]
    public void CompleteCatalogHasExactDeclarationParity()
    {
        IReadOnlyList<DotNetStandardLibraryModuleBinding> bindings =
        [
            .. DotNetStandardLibraryModules.Deterministic,
            DotNetStandardLibraryModules.CreateRandom(new RandomStandardLibraryOptions()),
            DotNetStandardLibraryModules.CreateClock(new ClockStandardLibraryOptions()),
            DotNetStandardLibraryModules.CreateGuid(new GuidStandardLibraryOptions()),
        ];

        DotNetStandardLibraryComposition composition = DotNetStandardLibraryComposer.Compose(bindings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bindings.Select(static binding => binding.Module), Is.EquivalentTo(StandardLibraryCatalog.All));
            Assert.That(composition.Globals, Has.Count.EqualTo(5));
            Assert.That(composition.Functions, Has.Count.EqualTo(66));
        }

        foreach (DotNetStandardLibraryModuleBinding binding in bindings)
        {
            Assert.That(binding.ModuleId, Is.EqualTo(binding.Module.Id));
            Assert.That(
                binding.Globals.Select(static implementation => implementation.Key),
                Is.EquivalentTo(binding.Module.Globals.Select(static declaration => declaration.Id))
            );
            Assert.That(
                binding.Functions.Select(static implementation => implementation.Id),
                Is.EquivalentTo(binding.Module.Functions.Select(static declaration => declaration.Id))
            );

            foreach (DotNetStandardLibraryFunction implementation in binding.Functions)
            {
                Assert.That(
                    implementation.ArgumentCount,
                    Is.EqualTo(binding.Module.Functions.Single(declaration => declaration.Id == implementation.Id).Parameters.Count)
                );
            }
        }
    }

    [Test]
    public void DeterministicBindingsAreSingletons()
    {
        Assert.That(DotNetStandardLibraryModules.MathBasic, Is.SameAs(DotNetStandardLibraryModules.MathBasic));
        Assert.That(DotNetStandardLibraryModules.StringTransform, Is.SameAs(DotNetStandardLibraryModules.StringTransform));
    }

    [Test]
    public void NondeterministicFactoriesCreateDistinctBindings()
    {
        Assert.That(
            DotNetStandardLibraryModules.CreateRandom(new RandomStandardLibraryOptions()),
            Is.Not.SameAs(DotNetStandardLibraryModules.CreateRandom(new RandomStandardLibraryOptions()))
        );
        Assert.That(
            DotNetStandardLibraryModules.CreateClock(new ClockStandardLibraryOptions()),
            Is.Not.SameAs(DotNetStandardLibraryModules.CreateClock(new ClockStandardLibraryOptions()))
        );
        Assert.That(
            DotNetStandardLibraryModules.CreateGuid(new GuidStandardLibraryOptions()),
            Is.Not.SameAs(DotNetStandardLibraryModules.CreateGuid(new GuidStandardLibraryOptions()))
        );
    }
}
