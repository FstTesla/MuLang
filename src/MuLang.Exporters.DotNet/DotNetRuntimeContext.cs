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
    private readonly IReadOnlyDictionary<string, DotNetProviderFunction> functions;
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
        : this(
            environment,
            globals,
            AdaptFunctions(functions),
            executionBudget,
            maximumTraversalDepth,
            cancellationToken,
            maximumUserFunctionCallDepth
        ) { }

    private DotNetRuntimeContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IReadOnlyDictionary<string, DotNetProviderFunction> functions,
        long? executionBudget,
        int maximumTraversalDepth,
        CancellationToken cancellationToken,
        int maximumUserFunctionCallDepth
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
        this.functions = functions;
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

    /// <summary>Creates a runtime context using context-aware provider functions.</summary>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="globals">The runtime global values, keyed by provider identifier.</param>
    /// <param name="functions">The context-aware runtime provider functions, keyed by provider identifier.</param>
    /// <param name="executionBudget">The maximum number of budgeted runtime operations, or <c>null</c> for no limit.</param>
    /// <param name="maximumTraversalDepth">The maximum depth used when traversing runtime values.</param>
    /// <param name="cancellationToken">The token used to cancel execution.</param>
    /// <param name="maximumUserFunctionCallDepth">The maximum user-defined function call depth.</param>
    /// <returns>A runtime context using the supplied context-aware provider functions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment" />, <paramref name="globals" />, or <paramref name="functions" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an execution limit is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when a runtime value or function identifier is duplicated.</exception>
    public static DotNetRuntimeContext Create(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<KeyValuePair<string, DotNetProviderFunction>> functions,
        long? executionBudget = null,
        int maximumTraversalDepth = 256,
        CancellationToken cancellationToken = default,
        int maximumUserFunctionCallDepth = 256
    )
    {
        return new DotNetRuntimeContext(
            environment,
            globals,
            FreezeFunctions(functions),
            executionBudget,
            maximumTraversalDepth,
            cancellationToken,
            maximumUserFunctionCallDepth
        );
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
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.MissingGlobal,
                $"Global '{id}' has no runtime value.",
                RuntimeErrorCategory.Environment,
                false,
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
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Global '{id}' does not contain a value of type '{expectedType.DisplayName}'.",
                RuntimeErrorCategory.RuntimeContract,
                false,
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

        if (!functions.TryGetValue(id, out DotNetProviderFunction? function))
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.MissingFunction,
                $"Function '{id}' has no runtime implementation.",
                RuntimeErrorCategory.Environment,
                false,
                span
            );
        }

        DotNetProviderInvocationContext invocationContext = new (this, span);

        try
        {
            if (!functionSymbols.TryGetValue(id, out FunctionSymbol? functionSymbol))
            {
                throw DotNetRuntimeErrorFactory.Create(
                    DotNetRuntimeErrorCodes.MissingFunction,
                    $"Function '{id}' is not declared by the runtime environment.",
                    RuntimeErrorCategory.Environment,
                    false,
                    span
                );
            }

            ValidateArguments(
                arguments,
                functionSymbol,
                span
            );
            object? result = function(invocationContext, arguments);

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
                throw DotNetRuntimeErrorFactory.Create(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"Provider function '{id}' returned a value incompatible with '{returnType.DisplayName}'.",
                    RuntimeErrorCategory.RuntimeContract,
                    false,
                    span
                );
            }

            return result;
        }
        catch (MuLangRuntimeException exception)
            when (exception.IsRuntimeGenerated)
        {
            throw;
        }
        catch (MuLangProviderException exception)
        {
            throw DotNetRuntimeErrorFactory.Create(
                exception.Code,
                exception.Message,
                RuntimeErrorCategory.Application,
                true,
                span,
                exception,
                exception.Cause,
                exception.Payload
            );
        }
        catch (OperationCanceledException exception)
            when (CancellationToken.IsCancellationRequested)
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.Cancelled,
                "MuLang execution was cancelled.",
                RuntimeErrorCategory.Cancellation,
                false,
                span,
                exception
            );
        }
        catch (MuLangRuntimeException exception)
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.ProviderFailure,
                $"Provider function '{id}' failed.",
                RuntimeErrorCategory.Provider,
                false,
                span,
                exception
            );
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.ProviderFailure,
                $"Provider function '{id}' failed.",
                RuntimeErrorCategory.Provider,
                false,
                span,
                exception
            );
        }
        finally
        {
            invocationContext.Complete();
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
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"Provider function '{function.Id}' received an invalid argument count.",
                RuntimeErrorCategory.RuntimeContract,
                false,
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
                throw DotNetRuntimeErrorFactory.Create(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"Provider function '{function.Id}' received an argument incompatible with '{parameterType.DisplayName}'.",
                    RuntimeErrorCategory.RuntimeContract,
                    false,
                    span
                );
            }
        }
    }

    internal void Consume(TextSpan span)
    {
        if (CancellationToken.IsCancellationRequested)
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.Cancelled,
                "MuLang execution was cancelled.",
                RuntimeErrorCategory.Cancellation,
                false,
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
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.BudgetExceeded,
                "The MuLang execution budget was exhausted.",
                RuntimeErrorCategory.Resource,
                false,
                span
            );
        }
    }

    private static IReadOnlyDictionary<string, DotNetProviderFunction> AdaptFunctions(
        IEnumerable<KeyValuePair<string, DotNetFunction>> functions
    )
    {
        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }

        return functions.ToFrozenDictionary(
            static pair => pair.Key,
            static pair =>
            {
                DotNetFunction function = pair.Value;
                return new DotNetProviderFunction((_, arguments) => function(arguments));
            },
            StringComparer.Ordinal
        );
    }

    private static IReadOnlyDictionary<string, DotNetProviderFunction> FreezeFunctions(
        IEnumerable<KeyValuePair<string, DotNetProviderFunction>> functions
    )
    {
        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }

        return functions.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal
        );
    }

    private static bool IsFatal(Exception exception)
    {
        return exception is
            OutOfMemoryException or
            StackOverflowException or
            AccessViolationException;
    }
}
