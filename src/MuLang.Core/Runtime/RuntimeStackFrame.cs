using MuLang.Core.Text;

namespace MuLang.Core.Runtime;

/// <summary>Represents a MuLang runtime stack frame.</summary>
public sealed class RuntimeStackFrame
{
    /// <summary>Initializes a new instance of the <see cref="RuntimeStackFrame" /> class.</summary>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="callSpan">The source span that entered the function.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="functionId" /> is null, empty, or whitespace.</exception>
    public RuntimeStackFrame(
        string functionId,
        TextSpan callSpan
    )
        : this(functionId, callSpan, null) { }

    /// <summary>Initializes a new instance of the <see cref="RuntimeStackFrame" /> class.</summary>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="callSpan">The source span that entered the function.</param>
    /// <param name="displayName">The diagnostic function display name, or <c>null</c> to use the identifier.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="functionId" /> or <paramref name="displayName" /> is empty or whitespace.</exception>
    public RuntimeStackFrame(
        string functionId,
        TextSpan callSpan,
        string? displayName
    )
        : this(functionId, callSpan, displayName, null) { }

    /// <summary>Initializes a new instance of the <see cref="RuntimeStackFrame" /> class with a materialized call location.</summary>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="callSpan">The source span that entered the function.</param>
    /// <param name="displayName">The diagnostic function display name, or <c>null</c> to use the identifier.</param>
    /// <param name="callLocation">The materialized call location, or <c>null</c> when unavailable.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="functionId" /> or <paramref name="displayName" /> is empty or whitespace, or <paramref name="callLocation" /> does not match <paramref name="callSpan" />.</exception>
    public RuntimeStackFrame(
        string functionId,
        TextSpan callSpan,
        string? displayName,
        TextLocation? callLocation
    )
    {
        if (string.IsNullOrWhiteSpace(functionId))
        {
            throw new ArgumentException(
                "Function identifier cannot be null or whitespace.",
                nameof(functionId)
            );
        }

        if (displayName is not null && string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Function display name cannot be empty or whitespace.",
                nameof(displayName)
            );
        }

        if (callLocation is { } value && value.Span != callSpan)
        {
            throw new ArgumentException(
                "The call location does not match the call span.",
                nameof(callLocation)
            );
        }

        FunctionId = functionId;
        CallSpan = callSpan;
        DisplayName = displayName ?? functionId;
        CallLocation = callLocation;
    }

    /// <summary>Gets the portable function identifier.</summary>
    public string FunctionId { get; }

    /// <summary>Gets the diagnostic function display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the source span that entered the function.</summary>
    public TextSpan CallSpan { get; }

    /// <summary>Gets the materialized call location, when available.</summary>
    public TextLocation? CallLocation { get; }
}
