namespace MuLang.Exporters.DotNet;

internal delegate object? DotNetUserFunction(
    DotNetRuntimeContext context,
    DotNetUserFunctionExecution execution,
    object?[] arguments
);
