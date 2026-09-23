namespace MuLang.Exporters.DotNet;

internal sealed class DotNetReadOnlyArrayValue : IDotNetReadOnlyArrayValue
{
    private readonly IReadOnlyList<object?> elements;

    public DotNetReadOnlyArrayValue(IEnumerable<object?> elements)
    {
        this.elements = [ .. elements ];
    }

    public object Identity => this;

    public int Count => elements.Count;

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
