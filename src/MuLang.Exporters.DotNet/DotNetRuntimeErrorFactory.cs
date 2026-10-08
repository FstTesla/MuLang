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
        RuntimeErrorData errorData = default
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
            errorData
        );
        return new MuLangRuntimeException(error, innerException, true);
    }
}
