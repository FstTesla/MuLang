using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Collections.Frozen;

namespace MuLang.Exporters.DotNet;

public sealed class DotNetRuntimeContext
{
    private readonly IReadOnlyDictionary<string, object?> globals;
    private readonly IReadOnlyDictionary<string, DotNetFunction> functions;
    private readonly bool hasExecutionBudget;
    private long remainingBudget;

    public DotNetRuntimeContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<KeyValuePair<string, DotNetFunction>> functions,
        long? executionBudget = null,
        int maximumTraversalDepth = 256,
        CancellationToken cancellationToken = default
    )
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (globals is null)
        {
            throw new ArgumentNullException(nameof(globals));
        }

        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }

        if (executionBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(executionBudget));
        }

        if (maximumTraversalDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTraversalDepth));
        }

        EnvironmentFingerprint = environment.Fingerprint;
        this.globals = globals.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal
        );
        this.functions = functions.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal
        );
        hasExecutionBudget = executionBudget is not null;
        remainingBudget = executionBudget ?? 0;
        MaximumTraversalDepth = maximumTraversalDepth;
        CancellationToken = cancellationToken;
    }

    public EnvironmentFingerprint EnvironmentFingerprint { get; }

    public CancellationToken CancellationToken { get; }

    public int MaximumTraversalDepth { get; }

    internal object? GetGlobal(string id, TypeSymbol expectedType, TextSpan span)
    {
        if (!globals.TryGetValue(id, out object? value))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.MissingGlobal,
                $"Global '{id}' has no runtime value.",
                span
            );
        }

        if (!DotNetRuntimeOperations.IsValueOfTypeDeep(
            this,
            value,
            expectedType,
            span
        ))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Global '{id}' does not contain a value of type '{expectedType.DisplayName}'.",
                span
            );
        }

        return value;
    }

    internal object? Invoke(
        string id,
        IReadOnlyList<object?> arguments,
        TypeSymbol returnType,
        TextSpan span
    )
    {
        Consume(span);

        if (!functions.TryGetValue(id, out DotNetFunction? function))
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.MissingFunction,
                $"Function '{id}' has no runtime implementation.",
                span
            );
        }

        try
        {
            object? result = function(arguments);

            if (
                returnType.Kind != TypeKind.Void &&
                !DotNetRuntimeOperations.IsValueOfTypeDeep(
                    this,
                    result,
                    returnType,
                    span
                )
            )
            {
                throw new MuLangRuntimeException(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"Provider function '{id}' returned a value incompatible with '{returnType.DisplayName}'.",
                    span
                );
            }

            return result;
        }
        catch (MuLangRuntimeException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
            when (CancellationToken.IsCancellationRequested)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.Cancelled,
                "MuLang execution was cancelled.",
                span,
                exception
            );
        }
        catch (Exception exception)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.ProviderFailure,
                $"Provider function '{id}' failed.",
                span,
                exception
            );
        }
    }

    internal void Consume(TextSpan span)
    {
        if (CancellationToken.IsCancellationRequested)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.Cancelled,
                "MuLang execution was cancelled.",
                span
            );
        }

        if (!hasExecutionBudget)
        {
            return;
        }

        long remaining = Interlocked.Decrement(ref remainingBudget);

        if (remaining < 0)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.BudgetExceeded,
                "The MuLang execution budget was exhausted.",
                span
            );
        }
    }
}
