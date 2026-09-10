using System.Diagnostics.CodeAnalysis;

namespace MuLang.Compiler.Binding;

internal sealed class BindingScope
{
    private readonly Dictionary<string, LocalSymbol> locals = new(StringComparer.Ordinal);

    public BindingScope(BindingScope? parent)
    {
        Parent = parent;
    }

    public BindingScope? Parent { get; }

    public IReadOnlyCollection<LocalSymbol> Locals => locals.Values;

    public bool TryDeclare(LocalSymbol local)
    {
        return locals.TryAdd(local.Name, local);
    }

    public bool ContainsLocal(string name)
    {
        return locals.ContainsKey(name);
    }

    public bool TryLookup(
        string name,
        [NotNullWhen(true)] out LocalSymbol? local
    )
    {
        BindingScope? scope = this;

        while (scope is not null)
        {
            if (scope.locals.TryGetValue(name, out local))
            {
                return true;
            }

            scope = scope.Parent;
        }

        local = null;
        return false;
    }
}
