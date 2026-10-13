using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.IR;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetUserFunctionExecution
{
    private readonly DotNetUserFunctionDispatcher dispatcher;
    private readonly Uri? sourceDocument;
    private readonly TextLineMap? lineMap;

    public DotNetUserFunctionExecution(
        DotNetUserFunctionDispatcher dispatcher,
        IrProgramAttributes? programAttributes
    )
    {
        this.dispatcher = dispatcher;
        sourceDocument = programAttributes?.SourceDocument;
        lineMap = programAttributes?.LineMap;
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

    public TextLocation? GetLocation(TextSpan span)
    {
        return lineMap?.GetLocation(span, sourceDocument);
    }

    public void Enrich(MuLangRuntimeException exception)
    {
        if (exception.Location is null && GetLocation(exception.Span) is { } location)
        {
            exception.Error.SetLocation(location);
        }
    }
}
