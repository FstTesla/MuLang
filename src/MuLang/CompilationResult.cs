using MuLang.Core.Diagnostics;
using MuLang.Exporters.DotNet;

namespace MuLang;

public sealed class CompilationResult
{
    internal CompilationResult(
        Func<DotNetRuntimeContext, object?>? compiledDelegate,
        DiagnosticCollection diagnostics
    )
    {
        Delegate = compiledDelegate;
        Diagnostics = diagnostics;
    }

    public Func<DotNetRuntimeContext, object?>? Delegate { get; }

    public DiagnosticCollection Diagnostics { get; }

    public bool IsSuccessful => Delegate is not null && !Diagnostics.HasErrors;
}
