using System.Diagnostics.CodeAnalysis;

namespace MuLang.Compiler.Binding;

internal sealed class BindingScope
{
    private readonly Dictionary<string, BoundVariableSymbol> variables = new (StringComparer.Ordinal);

    public BindingScope(BindingScope? parent)
    {
        Parent = parent;
    }

    public BindingScope? Parent { get; }

    public IReadOnlyCollection<BoundVariableSymbol> Variables => variables.Values;

    public bool TryDeclare(BoundVariableSymbol variable)
    {
        return variables.TryAdd(variable.Name, variable);
    }

    public bool ContainsVariable(string name)
    {
        return variables.ContainsKey(name);
    }

    public bool TryLookup(
        string name,
        [NotNullWhen(true)] out BoundVariableSymbol? variable
    )
    {
        BindingScope? scope = this;

        while (scope is not null)
        {
            if (scope.variables.TryGetValue(name, out variable))
            {
                return true;
            }

            scope = scope.Parent;
        }

        variable = null;
        return false;
    }
}
