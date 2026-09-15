namespace MuLang.Exporters.DotNet;

/// <summary>Represents an interface for an array value exposed to compiled MuLang code.</summary>
public interface IDotNetArrayValue
{
    /// <summary>Gets the stable identity used for reference tracking.</summary>
    object Identity { get; }

    /// <summary>Gets the number of elements.</summary>
    int Count { get; }

    /// <summary>Gets an element by index.</summary>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="value">When this method returns, contains the element value, if found.</param>
    /// <returns><c>true</c> if the element was retrieved; otherwise, <c>false</c>.</returns>
    bool TryGetElement(int index, out object? value);

    /// <summary>Sets an element by index.</summary>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="value">The element value.</param>
    /// <returns><c>true</c> if the element was set; otherwise, <c>false</c>.</returns>
    bool TrySetElement(int index, object? value);
}
