namespace MuLang.Exporters.DotNet;

internal sealed class DotNetArrayValue : IDotNetArrayValue, IDotNetReadOnlyArrayValue
{
    private readonly IList<object?> elements;

    public DotNetArrayValue(IEnumerable<object?> elements)
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

    public bool TrySetElement(int index, object? value)
    {
        if (index < 0 || index >= elements.Count)
        {
            return false;
        }

        elements[index] = value;
        return true;
    }
}
