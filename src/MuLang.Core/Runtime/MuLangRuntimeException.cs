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
        : this(
            new RuntimeError(
                code,
                message,
                RuntimeErrorCategory.Operation,
                true,
                span,
                [ ]
            ),
            innerException
        ) { }

    /// <summary>Initializes a new instance of the <see cref="MuLangRuntimeException" /> class.</summary>
    /// <param name="error">The structured runtime error.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <c>null</c>.</exception>
    public MuLangRuntimeException(RuntimeError error)
        : this(error, null) { }

    /// <summary>Initializes a new instance of the <see cref="MuLangRuntimeException" /> class.</summary>
    /// <param name="error">The structured runtime error.</param>
    /// <param name="innerException">The host exception that caused the runtime error, or <c>null</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <c>null</c>.</exception>
    public MuLangRuntimeException(RuntimeError error, Exception? innerException)
        : this(error, innerException, false) { }

    internal MuLangRuntimeException(
        RuntimeError error,
        Exception? innerException,
        bool isRuntimeGenerated
    )
        : base(
            (error ?? throw new ArgumentNullException(nameof(error))).Message,
            innerException
        )
    {
        Error = error;
        IsRuntimeGenerated = isRuntimeGenerated;
    }

    /// <summary>Gets the structured runtime error.</summary>
    public RuntimeError Error { get; }

    /// <summary>Gets the runtime error code.</summary>
    public string Code => Error.Code;

    /// <summary>Gets the source span associated with the error.</summary>
    public TextSpan Span => Error.Span;

    internal bool IsRuntimeGenerated { get; }

    internal void AddFrame(RuntimeStackFrame frame)
    {
        Error.AddFrame(frame);
    }
}
