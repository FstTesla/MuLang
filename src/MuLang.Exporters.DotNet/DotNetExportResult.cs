using MuLang.Core.Diagnostics;

namespace MuLang.Exporters.DotNet;

internal sealed record DotNetExportResult(
    Func<DotNetRuntimeContext, object?>? Delegate,
    DiagnosticCollection Diagnostics
);
