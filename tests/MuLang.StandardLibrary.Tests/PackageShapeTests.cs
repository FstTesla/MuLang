using MuLang.Core.Environment;
using System.Reflection;

namespace MuLang.StandardLibrary.Tests;

public sealed class PackageShapeTests
{
    [Test]
    public void ReferencesNoOtherMuLangPackageBeyondCore()
    {
        Assembly assembly = typeof(StandardLibraryModule).Assembly;

        Assert.That(
            assembly.GetReferencedAssemblies()
                .Select(static reference => reference.Name)
                .Where(static name => name?.StartsWith("MuLang.", StringComparison.Ordinal) is true),
            Is.EqualTo([ "MuLang.Core" ])
        );
    }

    [Test]
    public void ImportsNothingImplicitly()
    {
        EnvironmentSchema coreEnvironment = new EnvironmentBuilder().Build();
        StandardLibrarySelection selection = StandardLibrarySelection.Create([ ]);
        EnvironmentSchema emptyComposition = StandardLibraryComposer.Compose(selection);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coreEnvironment.Types, Is.Empty);
            Assert.That(coreEnvironment.Globals, Is.Empty);
            Assert.That(coreEnvironment.Functions, Is.Empty);
            Assert.That(emptyComposition.Types, Is.Empty);
            Assert.That(emptyComposition.Globals, Is.Empty);
            Assert.That(emptyComposition.Functions, Is.Empty);
        }
    }
}
