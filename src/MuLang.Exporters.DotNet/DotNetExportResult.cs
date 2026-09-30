using MuLang.Core.Diagnostics;
using MuLang.Core.Runtime;

namespace MuLang.Exporters.DotNet;

/// <summary>Represents the result of exporting portable MuLang IR to a .NET delegate.</summary>
/// <param name="Delegate">The exported delegate, or <c>null</c> when export failed.</param>
/// <param name="Diagnostics">The diagnostics produced during validation and export.</param>
public sealed record DotNetExportResult(
    Func<DotNetRuntimeContext, object?>? Delegate,
    DiagnosticCollection Diagnostics
)
{
    /// <summary>Gets the exported result-returning delegate, or <c>null</c> when export failed.</summary>
    public Func<DotNetRuntimeContext, ExecutionResult>? ExecutionDelegate =>
        Delegate is null
            ? null
            : context => Execute(Delegate, context);

    private static ExecutionResult Execute(
        Func<DotNetRuntimeContext, object?> compiledDelegate,
        DotNetRuntimeContext context
    )
    {
        try
        {
            return ExecutionResult.Success(compiledDelegate(context));
        }
        catch (MuLangRuntimeException exception)
        {
            return ExecutionResult.Failure(
                exception.Error,
                exception.InnerException
            );
        }
    }
}
