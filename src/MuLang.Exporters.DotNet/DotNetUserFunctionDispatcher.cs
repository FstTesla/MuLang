using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetUserFunctionDispatcher
{
    private readonly IReadOnlyDictionary<string, IrFunction> functions;
    private readonly IReadOnlyDictionary<string, DotNetUserFunction> delegates;

    public DotNetUserFunctionDispatcher(
        IEnumerable<IrFunction> functions,
        IReadOnlyDictionary<string, DotNetUserFunction> delegates
    )
    {
        this.functions = functions.ToDictionary(
            static function => function.Id,
            StringComparer.Ordinal
        );
        this.delegates = delegates;
    }

    public DotNetUserFunctionExecution CreateExecution()
    {
        return new DotNetUserFunctionExecution(this);
    }

    public object? Invoke(
        DotNetUserFunctionExecution execution,
        DotNetRuntimeContext context,
        string functionId,
        object?[] arguments,
        TextSpan span
    )
    {
        context.Consume(span);

        if (
            !functions.TryGetValue(functionId, out IrFunction? function) ||
            !delegates.TryGetValue(functionId, out DotNetUserFunction? functionDelegate)
        )
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.MissingUserFunction,
                $"User function '{functionId}' is not available.",
                RuntimeErrorCategory.Environment,
                false,
                span
            );
        }

        IReadOnlyList<IrSlot> parameters =
            [ .. function.Slots.Where(static slot => slot.Kind == IrSlotKind.Parameter) ];

        if (arguments.Length != parameters.Count)
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                $"User function '{functionId}' received an invalid argument count.",
                RuntimeErrorCategory.RuntimeContract,
                false,
                span
            );
        }

        for (int index = 0; index < parameters.Count; index++)
        {
            if (!DotNetRuntimeOperations.IsValueOfTypeDeep(
                    context,
                    arguments[index],
                    parameters[index].Type,
                    span
                ))
            {
                throw DotNetRuntimeErrorFactory.Create(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"Argument {index + 1} of user function '{functionId}' is incompatible with '{parameters[index].Type.DisplayName}'.",
                    RuntimeErrorCategory.RuntimeContract,
                    false,
                    span
                );
            }
        }

        int nextDepth = execution.CallDepth + 1;

        if (nextDepth > context.MaximumUserFunctionCallDepth)
        {
            throw DotNetRuntimeErrorFactory.Create(
                DotNetRuntimeErrorCodes.CallDepthExceeded,
                "The maximum user-function call depth was exceeded.",
                RuntimeErrorCategory.Resource,
                false,
                span
            );
        }

        execution.CallDepth = nextDepth;

        try
        {
            object? result = functionDelegate(context, execution, arguments);

            if (
                function.ReturnType.Kind != TypeKind.Void &&
                !DotNetRuntimeOperations.IsValueOfTypeDeep(
                    context,
                    result,
                    function.ReturnType,
                    span
                )
            )
            {
                throw DotNetRuntimeErrorFactory.Create(
                    DotNetRuntimeErrorCodes.InvalidRuntimeValue,
                    $"User function '{functionId}' returned a value incompatible with '{function.ReturnType.DisplayName}'.",
                    RuntimeErrorCategory.RuntimeContract,
                    false,
                    span
                );
            }

            return result;
        }
        catch (MuLangRuntimeException exception)
        {
            exception.AddFrame(
                new RuntimeStackFrame(functionId, span)
            );
            throw;
        }
        finally
        {
            execution.CallDepth = nextDepth - 1;
        }
    }
}
