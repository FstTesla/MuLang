using MuLang.Core.Text;

namespace MuLang.Core.Diagnostics;

/// <summary>Represents a diagnostic produced while compiling MuLang source code.</summary>
public sealed record Diagnostic
{
    /// <summary>Initializes a new instance of the <see cref="Diagnostic" /> class.</summary>
    /// <param name="code">The error or diagnostic code.</param>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="span">The source span.</param>
    /// <param name="message">The error or diagnostic message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> or <paramref name="message" /> is null, empty, or whitespace.</exception>
    public Diagnostic(
        string code,
        DiagnosticSeverity severity,
        DiagnosticCategory category,
        TextSpan span,
        string message
    )
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Diagnostic code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic message cannot be null or whitespace.", nameof(message));
        }

        Code = code;
        Severity = severity;
        Category = category;
        Span = span;
        Message = message;
    }

    /// <summary>Gets the diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the diagnostic severity.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Gets the compiler phase that produced the diagnostic.</summary>
    public DiagnosticCategory Category { get; }

    /// <summary>Gets the source span associated with the diagnostic.</summary>
    public TextSpan Span { get; }

    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; }
}
