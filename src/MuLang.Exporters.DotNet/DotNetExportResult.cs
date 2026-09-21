using MuLang.Core.Diagnostics;

namespace MuLang.Exporters.DotNet;

/// <summary>Represents the result of exporting portable MuLang IR to a .NET delegate.</summary>
/// <param name="Delegate">The exported delegate, or <c>null</c> when export failed.</param>
/// <param name="Diagnostics">The diagnostics produced during validation and export.</param>
public sealed record DotNetExportResult(
    Func<DotNetRuntimeContext, object?>? Delegate,
    DiagnosticCollection Diagnostics
);
