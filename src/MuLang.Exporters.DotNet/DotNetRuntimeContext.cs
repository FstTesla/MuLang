using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Collections.Frozen;

namespace MuLang.Exporters.DotNet;

/// <summary>Provides runtime values, functions, and execution limits for compiled MuLang code.</summary>
public sealed class DotNetRuntimeContext
{
    private readonly IReadOnlyDictionary<string, object?> globals;
    private readonly IReadOnlyDictionary<string, DotNetFunction> functions;
    private readonly IReadOnlyDictionary<string, FunctionSymbol> functionSymbols;
    private readonly bool hasExecutionBudget;
    private long remainingBudget;

    /// <summary>Initializes a new instance of the <see cref="DotNetRuntimeContext" /> class.</summary>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="globals">The runtime global values, keyed by provider identifier.</param>
    /// <param name="functions">The runtime provider functions, keyed by provider identifier.</param>
    /// <param name="executionBudget">The maximum number of budgeted runtime operations, or <c>null</c> for no limit.</param>
    /// <param name="maximumTraversalDepth">The maximum depth used when traversing runtime values.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <param name="maximumUserFunctionCallDepth">The maximum user-defined function call depth.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment" />, <paramref name="globals" />, or <paramref name="functions" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an execution limit is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when a runtime value or function identifier is duplicated.</exception>
    public DotNetRuntimeContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<KeyValuePair<string, DotNetFunction>> functions,
        long? executionBudget = null,
        int maximumTraversalDepth = 256,
        CancellationToken cancellationToken = default,
        int maximumUserFunctionCallDepth = 256
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

        if (maximumUserFunctionCallDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumUserFunctionCallDepth));
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
        functionSymbols = environment.Functions.ToFrozenDictionary(
            static function => function.Id,
            StringComparer.Ordinal
        );
        hasExecutionBudget = executionBudget is not null;
        remainingBudget = executionBudget ?? 0;
        MaximumTraversalDepth = maximumTraversalDepth;
        MaximumUserFunctionCallDepth = maximumUserFunctionCallDepth;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the environment fingerprint.</summary>
    public EnvironmentFingerprint EnvironmentFingerprint { get; }

    /// <summary>Gets the cancellation token.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets the maximum traversal depth.</summary>
    public int MaximumTraversalDepth { get; }

    /// <summary>Gets the maximum user function call depth.</summary>
    public int MaximumUserFunctionCallDepth { get; }

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
            if (!functionSymbols.TryGetValue(id, out FunctionSymbol? functionSymbol))
            {
                throw new MuLangRuntimeException(
                    DotNetRuntimeErrorCodes.MissingFunction,
                    $"Function '{id}' is not declared by the runtime environment.",
                    span
                );
            }

            ValidateArguments(
                arguments,
                functionSymbol,
                span
            );
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

    private void ValidateArguments(
        IReadOnlyList<object?> arguments,
        FunctionSymbol function,
        TextSpan span
    )
    {
        if (arguments.Count != function.Parameters.Count)
        {
            throw new MuLangRuntimeException(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Provider function '{function.Id}' received an invalid argument count.",
                span
            );
        }

        for (int index = 0; index < arguments.Count; index++)
        {
            object? argument = arguments[index];
            TypeSymbol parameterType = function.Parameters[index].Type;

            if (!DotNetRuntimeOperations.IsValueOfTypeDeep(
                    this,
                    argument,
                    parameterType,
                    span
                ))
            {
                throw new MuLangRuntimeException(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"Provider function '{function.Id}' received an argument incompatible with '{parameterType.DisplayName}'.",
                    span
                );
            }
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
