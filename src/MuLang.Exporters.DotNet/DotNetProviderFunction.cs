namespace MuLang.Exporters.DotNet;

/// <summary>Represents a context-aware .NET implementation of a MuLang provider function.</summary>
/// <param name="context">The provider invocation context.</param>
/// <param name="arguments">The function arguments.</param>
/// <returns>The function result.</returns>
public delegate object? DotNetProviderFunction(
    DotNetProviderInvocationContext context,
    IReadOnlyList<object?> arguments
);
