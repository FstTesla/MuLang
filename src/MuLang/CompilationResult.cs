using MuLang.Core.Diagnostics;
using MuLang.Exporters.DotNet;

namespace MuLang;

/// <summary>Represents the result of compiling MuLang source code.</summary>
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

    /// <summary>Gets the compiled delegate, or <c>null</c> when compilation failed.</summary>
    public Func<DotNetRuntimeContext, object?>? Delegate { get; }

    /// <summary>Gets the diagnostics produced by compilation.</summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>Gets a value indicating whether compilation produced an executable delegate without errors.</summary>
    public bool IsSuccessful => Delegate is not null && !Diagnostics.HasErrors;
}
