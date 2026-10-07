using MuLang.Core.Text;
using System.Runtime.CompilerServices;

namespace MuLang.Core.Runtime;

/// <summary>Represents a structured MuLang runtime error.</summary>
public sealed class RuntimeError
{
    private readonly List<RuntimeStackFrame> frames;

    /// <summary>Initializes a new instance of the <see cref="RuntimeError" /> class.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="category">The error category.</param>
    /// <param name="isCatchable">A value indicating whether source-level recovery may catch the error.</param>
    /// <param name="span">The source span associated with the failing operation.</param>
    /// <param name="frames">The MuLang stack frames, ordered from innermost to outermost.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="data">The optional application payload.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> is null, empty, or whitespace, or <paramref name="category" /> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message" /> or <paramref name="frames" /> is <c>null</c>, or a frame is <c>null</c>.</exception>
#pragma warning disable RS0027
    [Obsolete("Use RuntimeError(string, string, RuntimeErrorCategory, bool, TextSpan, IEnumerable<RuntimeStackFrame>, RuntimeError?, RuntimeErrorData) instead.")]
    public RuntimeError(
        string code,
        string message,
        RuntimeErrorCategory category,
        bool isCatchable,
        TextSpan span,
        IEnumerable<RuntimeStackFrame> frames,
        RuntimeError? cause = null,
        object? data = null
    )
        : this(
            code,
            message,
            category,
            isCatchable,
            span,
            frames,
            cause,
            data is null
                ? RuntimeErrorData.Absent
                : RuntimeErrorData.Present(data)
        ) { }
#pragma warning restore RS0027

    /// <summary>Initializes a new instance of the <see cref="RuntimeError" /> class with an explicit optional payload.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="category">The error category.</param>
    /// <param name="isCatchable">A value indicating whether source-level recovery may catch the error.</param>
    /// <param name="span">The source span associated with the failing operation.</param>
    /// <param name="frames">The MuLang stack frames, ordered from innermost to outermost.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="data">The optional application payload.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> is null, empty, or whitespace, or <paramref name="category" /> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message" /> or <paramref name="frames" /> is <c>null</c>, or a frame is <c>null</c>.</exception>
    [OverloadResolutionPriority(1)]
    public RuntimeError(
        string code,
        string message,
        RuntimeErrorCategory category,
        bool isCatchable,
        TextSpan span,
        IEnumerable<RuntimeStackFrame> frames,
        RuntimeError? cause,
        RuntimeErrorData data
    )
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Runtime error code cannot be null or whitespace.",
                nameof(code)
            );
        }

        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentException(
                "Runtime error category is invalid.",
                nameof(category)
            );
        }

        if (frames is null)
        {
            throw new ArgumentNullException(nameof(frames));
        }

        List<RuntimeStackFrame> frameList = [ ];

        foreach (RuntimeStackFrame frame in frames)
        {
            frameList.Add(
                frame ?? throw new ArgumentNullException(
                    nameof(frames),
                    "Runtime error frames cannot contain null."
                )
            );
        }

        Code = code;
        Message = message;
        Category = category;
        IsCatchable = isCatchable;
        Span = span;
        this.frames = frameList;
        Frames = frameList.AsReadOnly();
        Cause = cause;
        Data = data.Value;
        ErrorData = data;
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the error message.</summary>
    public string Message { get; }

    /// <summary>Gets the error category.</summary>
    public RuntimeErrorCategory Category { get; }

    /// <summary>Gets a value indicating whether source-level recovery may catch the error.</summary>
    public bool IsCatchable { get; }

    /// <summary>Gets the source span associated with the failing operation.</summary>
    public TextSpan Span { get; }

    /// <summary>Gets the MuLang stack frames, ordered from innermost to outermost.</summary>
    public IReadOnlyList<RuntimeStackFrame> Frames { get; }

    /// <summary>Gets the optional public MuLang error cause.</summary>
    public RuntimeError? Cause { get; }

    /// <summary>Gets the optional application payload.</summary>
    public object? Data { get; }

    /// <summary>Gets the optional application payload with explicit presence.</summary>
    public RuntimeErrorData ErrorData { get; }

    internal void AddFrame(RuntimeStackFrame frame)
    {
        frames.Add(frame);
    }
}
