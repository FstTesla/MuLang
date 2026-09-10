namespace MuLang.Exporters.DotNet;

public delegate object? DotNetFunction(
    DotNetRuntimeContext context,
    IReadOnlyList<object?> arguments
);
