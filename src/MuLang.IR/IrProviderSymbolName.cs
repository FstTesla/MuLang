namespace MuLang.IR;

/// <summary>Represents a diagnostic language name associated with a provider symbol identifier.</summary>
/// <param name="Id">The provider symbol identifier.</param>
/// <param name="Name">The language-facing name.</param>
public sealed record IrProviderSymbolName(string Id, string Name);
