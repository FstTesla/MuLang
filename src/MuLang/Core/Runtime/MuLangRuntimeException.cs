using MuLang.Core.Text;

namespace MuLang.Core.Runtime;

/// <summary>Represents an error that occurs while executing compiled MuLang code.</summary>
public sealed class MuLangRuntimeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MuLangRuntimeException" /> class.</summary>
    /// <param name="code">The error or diagnostic code.</param>
    /// <param name="message">The error or diagnostic message.</param>
    /// <param name="span">The source span.</param>
    /// <param name="innerException">The exception that caused the runtime error, or <c>null</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> is null, empty, or whitespace.</exception>
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

    /// <summary>Gets the runtime error code.</summary>
    public string Code { get; }

    /// <summary>Gets the source span associated with the error.</summary>
    public TextSpan Span { get; }
}
