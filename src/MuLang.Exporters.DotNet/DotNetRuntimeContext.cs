using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Collections.Frozen;

namespace MuLang.Exporters.DotNet;

public sealed class DotNetRuntimeContext
{
    private readonly FrozenDictionary<string, object?> globals;
    private readonly FrozenDictionary<string, DotNetFunction> functions;
    private long remainingBudget;

    public DotNetRuntimeContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<KeyValuePair<string, DotNetFunction>> functions,
        long executionBudget = long.MaxValue,
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
        remainingBudget = executionBudget;
        CancellationToken = cancellationToken;
    }

    public EnvironmentFingerprint EnvironmentFingerprint { get; }

    public CancellationToken CancellationToken { get; }

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

        if (!DotNetRuntimeOperations.IsValueOfTypeShallow(value, expectedType))
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
            object? result = function(this, arguments);

            if (
                returnType.Kind != TypeKind.Void &&
                !DotNetRuntimeOperations.IsValueOfTypeShallow(result, returnType)
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

        if (remainingBudget == long.MaxValue)
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
