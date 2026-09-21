using System.Reflection;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class PackageShapeTests
{
    [Test]
    public void PlaceholderAssemblyExposesNoPrematureApi()
    {
        Assembly assembly = Assembly.Load("MuLang.StandardLibrary.DotNet");

        Assert.That(assembly.GetExportedTypes(), Is.Empty);
    }
}
