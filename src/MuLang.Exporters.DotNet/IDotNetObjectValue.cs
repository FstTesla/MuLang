namespace MuLang.Exporters.DotNet;

public interface IDotNetObjectValue
{
    object Identity { get; }

    IReadOnlyCollection<string> PropertyNames { get; }

    bool TryGetProperty(string name, out object? value);

    bool TrySetProperty(string name, object? value);

    bool TryRemoveProperty(string name);
}
