using MuLang.Core.Text;

namespace MuLang.Core.Diagnostics;

public sealed record Diagnostic
{
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

    public string Code { get; }

    public DiagnosticSeverity Severity { get; }

    public DiagnosticCategory Category { get; }

    public TextSpan Span { get; }

    public string Message { get; }
}
