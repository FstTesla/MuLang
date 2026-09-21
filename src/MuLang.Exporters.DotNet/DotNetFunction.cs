namespace MuLang.Exporters.DotNet;

/// <summary>Represents a .NET implementation of a MuLang provider function.</summary>
/// <param name="arguments">The function arguments.</param>
/// <returns>The function result.</returns>
public delegate object? DotNetFunction(IReadOnlyList<object?> arguments);
