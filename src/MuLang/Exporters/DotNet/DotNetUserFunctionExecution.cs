using MuLang.Core.Text;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetUserFunctionExecution
{
    private readonly DotNetUserFunctionDispatcher dispatcher;

    public DotNetUserFunctionExecution(DotNetUserFunctionDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public int CallDepth { get; set; }

    public object? Invoke(
        DotNetRuntimeContext context,
        string functionId,
        object?[] arguments,
        TextSpan span
    )
    {
        return dispatcher.Invoke(this, context, functionId, arguments, span);
    }
}
