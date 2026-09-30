using MuLang.Core.Runtime;
using MuLang.Core.Text;

namespace MuLang.Exporters.DotNet;

internal static class DotNetRuntimeErrorFactory
{
    public static MuLangRuntimeException Create(
        string code,
        string message,
        RuntimeErrorCategory category,
        bool isCatchable,
        TextSpan span,
        Exception? innerException = null,
        RuntimeError? cause = null,
        object? data = null
    )
    {
        RuntimeError error = new (
            code,
            message,
            category,
            isCatchable,
            span,
            [ ],
            cause,
            data
        );
        return new MuLangRuntimeException(error, innerException, true);
    }
}
