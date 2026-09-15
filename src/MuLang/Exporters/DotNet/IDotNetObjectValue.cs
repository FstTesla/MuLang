namespace MuLang.Exporters.DotNet;

/// <summary>Represents an interface for an object value exposed to compiled MuLang code.</summary>
public interface IDotNetObjectValue
{
    /// <summary>Gets the stable identity used for reference tracking.</summary>
    object Identity { get; }

    /// <summary>Gets the available property names.</summary>
    IReadOnlyCollection<string> PropertyNames { get; }

    /// <summary>Gets a property value by name.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">When this method returns, contains the property value, if found.</param>
    /// <returns><c>true</c> if the property was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetProperty(string name, out object? value);

    /// <summary>Sets a property value by name.</summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns><c>true</c> if the property was set; otherwise, <c>false</c>.</returns>
    bool TrySetProperty(string name, object? value);

    /// <summary>Removes a property by name.</summary>
    /// <param name="name">The property name.</param>
    /// <returns><c>true</c> if the property was removed; otherwise, <c>false</c>.</returns>
    bool TryRemoveProperty(string name);
}
