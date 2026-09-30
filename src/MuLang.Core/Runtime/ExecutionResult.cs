namespace MuLang.Core.Runtime;

/// <summary>Represents the result of executing MuLang code.</summary>
public sealed class ExecutionResult
{
    private ExecutionResult(
        object? value,
        RuntimeError? error,
        Exception? hostException
    )
    {
        Value = value;
        Error = error;
        HostException = hostException;
    }

    /// <summary>Gets a value indicating whether execution completed successfully.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets the execution value, or <c>null</c> when execution failed or completed with a null value.</summary>
    public object? Value { get; }

    /// <summary>Gets the runtime error, or <c>null</c> when execution completed successfully.</summary>
    public RuntimeError? Error { get; }

    /// <summary>Gets the underlying host failure, or <c>null</c> when unavailable.</summary>
    public Exception? HostException { get; }

    internal static ExecutionResult Success(object? value)
    {
        return new ExecutionResult(value, null, null);
    }

    internal static ExecutionResult Failure(
        RuntimeError error,
        Exception? hostException
    )
    {
        return new ExecutionResult(
            null,
            error ?? throw new ArgumentNullException(nameof(error)),
            hostException
        );
    }
}
