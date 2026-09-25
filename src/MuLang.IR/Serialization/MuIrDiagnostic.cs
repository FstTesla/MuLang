using MuLang.Core.Diagnostics;

namespace MuLang.IR.Serialization;

/// <summary>Represents a diagnostic produced while reading a MuIR document.</summary>
public sealed class MuIrDiagnostic
{
    /// <summary>Initializes a new instance of the <see cref="MuIrDiagnostic" /> class.</summary>
    /// <param name="code">The diagnostic code.</param>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="offset">The zero-based document offset.</param>
    /// <param name="length">The diagnostic span length.</param>
    /// <param name="line">The one-based line number.</param>
    /// <param name="column">The one-based column number.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> or <paramref name="message" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the severity or a location value is invalid.</exception>
    public MuIrDiagnostic(
        string code,
        DiagnosticSeverity severity,
        int offset,
        int length,
        int line,
        int column,
        string message
    )
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Diagnostic code cannot be null or whitespace.",
                nameof(code)
            );
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (line <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (column <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Diagnostic message cannot be null or whitespace.",
                nameof(message)
            );
        }

        Code = code;
        Severity = severity;
        Offset = offset;
        Length = length;
        Line = line;
        Column = column;
        Message = message;
    }

    /// <summary>Gets the diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the diagnostic severity.</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>Gets the zero-based document offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the diagnostic span length.</summary>
    public int Length { get; }

    /// <summary>Gets the one-based line number.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based column number.</summary>
    public int Column { get; }

    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; }
}
