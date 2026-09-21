using System.Reflection;

namespace MuLang.StandardLibrary.Tests;

public sealed class PackageShapeTests
{
    [Test]
    public void PlaceholderAssemblyExposesNoPrematureApi()
    {
        Assembly assembly = Assembly.Load("MuLang.StandardLibrary");

        Assert.That(assembly.GetExportedTypes(), Is.Empty);
    }
}
