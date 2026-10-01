using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents a read-only array value returned by standard-library functions.</summary>
internal sealed class DotNetStandardLibraryReadOnlyArray : IDotNetReadOnlyArrayValue
{
    private readonly IReadOnlyList<object?> elements;

    /// <summary>Initializes a new instance of the <see cref="DotNetStandardLibraryReadOnlyArray" /> class.</summary>
    /// <param name="elements">The array elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements" /> is <c>null</c>.</exception>
    public DotNetStandardLibraryReadOnlyArray(IEnumerable<object?> elements)
    {
        this.elements = [ .. elements ?? throw new ArgumentNullException(nameof(elements)) ];
    }

    /// <inheritdoc />
    public object Identity => this;

    /// <inheritdoc />
    public int Count => elements.Count;

    /// <inheritdoc />
    public bool TryGetElement(int index, out object? value)
    {
        if (index < 0 || index >= elements.Count)
        {
            value = null;
            return false;
        }

        value = elements[index];
        return true;
    }
}
