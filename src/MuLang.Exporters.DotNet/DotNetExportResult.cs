using MuLang.Core.Diagnostics;
using MuLang.Core.Runtime;
using System.Runtime.ExceptionServices;

namespace MuLang.Exporters.DotNet;

/// <summary>Represents the result of exporting portable MuLang IR to a .NET delegate.</summary>
/// <param name="ExecutionDelegate">The exported result-returning delegate, or <c>null</c> when export failed.</param>
/// <param name="Diagnostics">The diagnostics produced during validation and export.</param>
public sealed record DotNetExportResult(
    Func<DotNetRuntimeContext, ExecutionResult>? ExecutionDelegate,
    DiagnosticCollection Diagnostics
)
{
    /// <summary>Gets the exported exception-based delegate, or <c>null</c> when export failed.</summary>
    public Func<DotNetRuntimeContext, object?>? Delegate =>
        ExecutionDelegate is null
            ? null
            : context => Execute(ExecutionDelegate, context);

    private static object? Execute(
        Func<DotNetRuntimeContext, ExecutionResult> executionDelegate,
        DotNetRuntimeContext context
    )
    {
        ExecutionResult result = executionDelegate(context);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        if (result.RuntimeException is not null)
        {
            ExceptionDispatchInfo.Capture(result.RuntimeException).Throw();
        }

        throw new MuLangRuntimeException(
            result.Error ??
            throw new InvalidOperationException(
                "A failed execution result does not contain a runtime error."
            ),
            result.HostException
        );
    }
}
