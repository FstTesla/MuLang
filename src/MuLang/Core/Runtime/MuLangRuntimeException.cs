using MuLang.Core.Text;

namespace MuLang.Core.Runtime;

public sealed class MuLangRuntimeException : Exception
{
    public MuLangRuntimeException(
        string code,
        string message,
        TextSpan span,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Runtime error code cannot be null or whitespace.",
                nameof(code)
            );
        }

        Code = code;
        Span = span;
    }

    public string Code { get; }

    public TextSpan Span { get; }
}
