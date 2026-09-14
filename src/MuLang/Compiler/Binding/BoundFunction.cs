namespace MuLang.Compiler.Binding;

internal sealed record BoundFunction(
    UserFunctionSymbol Symbol,
    IReadOnlyList<BoundStatement> Statements
);
