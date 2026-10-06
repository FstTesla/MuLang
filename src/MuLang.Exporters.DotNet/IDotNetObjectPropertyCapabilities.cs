namespace MuLang.Exporters.DotNet;

/// <summary>Represents an interface for per-property mutation capabilities exposed by an object adapter.</summary>
public interface IDotNetObjectPropertyCapabilities
{
    /// <summary>Determines whether a declared property is read-only.</summary>
    /// <param name="name">The property name.</param>
    /// <returns><c>true</c> if the property is read-only; otherwise, <c>false</c>.</returns>
    bool IsPropertyReadOnly(string name);
}
