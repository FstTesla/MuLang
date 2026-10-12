using MuLang.Core.Text;

namespace MuLang.IR;

/// <summary>Represents diagnostic attributes associated with an IR program.</summary>
/// <param name="SourceDocument">The source document name or URI, when available.</param>
/// <param name="ProviderGlobalNames">The referenced provider-global names.</param>
/// <param name="ProviderFunctionNames">The referenced provider-function names.</param>
public sealed record IrProgramAttributes(
    string? SourceDocument,
    IReadOnlyList<IrProviderSymbolName> ProviderGlobalNames,
    IReadOnlyList<IrProviderSymbolName> ProviderFunctionNames
)
{
    /// <summary>Gets an empty program attribute set.</summary>
    public static IrProgramAttributes Empty { get; } = new (null, [ ], [ ]);

    /// <summary>Gets the source line map, when available.</summary>
    public TextLineMap? LineMap { get; init; }
}
