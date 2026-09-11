namespace MuLang.Exporters.DotNet;

internal sealed class DotNetObjectValue : IDotNetObjectValue
{
    private readonly Dictionary<string, object?> properties;

    public DotNetObjectValue(IEnumerable<KeyValuePair<string, object?>> properties)
    {
        this.properties = properties.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal
        );
    }

    public object Identity => this;

    public IReadOnlyCollection<string> PropertyNames => properties.Keys;

    public bool TryGetProperty(string name, out object? value)
    {
        return properties.TryGetValue(name, out value);
    }

    public bool TrySetProperty(string name, object? value)
    {
        properties[name] = value;
        return true;
    }

    public bool TryRemoveProperty(string name)
    {
        return properties.Remove(name);
    }
}
