using MuLang.Compiler.Syntax;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed class UserFunctionSymbol
{
    public UserFunctionSymbol(
        string id,
        string name,
        IReadOnlyList<UserParameterSymbol> parameters,
        TypeSymbol returnType,
        FunctionDeclarationSyntax declaration,
        int ordinal
    )
    {
        Id = id;
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        Declaration = declaration;
        Ordinal = ordinal;
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<UserParameterSymbol> Parameters { get; }

    public TypeSymbol ReturnType { get; }

    public FunctionDeclarationSyntax Declaration { get; }

    public int Ordinal { get; }
}
