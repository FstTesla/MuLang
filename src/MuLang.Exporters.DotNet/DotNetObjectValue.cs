using MuLang.Core.Types;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetObjectValue :
    IDotNetObjectValue,
    IDotNetObjectPropertyCapabilities
{
    private readonly ObjectTypeSymbol type;
    private readonly Dictionary<string, object?> properties;

    public DotNetObjectValue(
        ObjectTypeSymbol type,
        IEnumerable<KeyValuePair<string, object?>> properties
    )
    {
        this.type = type;
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
        if (type.TryGetProperty(name, out ObjectPropertySymbol? property))
        {
            if (property.IsReadOnly)
            {
                return false;
            }
        }
        else if (!type.IsOpen)
        {
            return false;
        }

        properties[name] = value;
        return true;
    }

    public bool TryRemoveProperty(string name)
    {
        if (
            type.TryGetProperty(name, out ObjectPropertySymbol? property) &&
            (!property.IsOptional || property.IsReadOnly)
        )
        {
            return false;
        }

        return properties.Remove(name);
    }

    public bool IsPropertyReadOnly(string name)
    {
        return type.TryGetProperty(name, out ObjectPropertySymbol? property) &&
            property.IsReadOnly;
    }
}
