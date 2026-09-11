namespace MuLang.Exporters.DotNet;

public interface IDotNetArrayValue
{
    object Identity { get; }

    int Count { get; }

    bool TryGetElement(int index, out object? value);

    bool TrySetElement(int index, object? value);
}
