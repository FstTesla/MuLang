using MuLang.Core.Runtime;

namespace MuLang.Exporters.DotNet;

/// <summary>Represents an expected application failure reported by a .NET provider function.</summary>
public sealed class MuLangProviderException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MuLangProviderException" /> class.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="data">The optional application payload.</param>
    /// <param name="innerException">The host exception that caused the application failure, or <c>null</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code" /> is null, empty, or whitespace.</exception>
    public MuLangProviderException(
        string code,
        string message,
        RuntimeError? cause = null,
        object? data = null,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Application error code cannot be null or whitespace.",
                nameof(code)
            );
        }

        Code = code;
        Cause = cause;
        Payload = data;
    }

    /// <summary>Gets the stable application error code.</summary>
    public string Code { get; }

    /// <summary>Gets the optional public MuLang error cause.</summary>
    public RuntimeError? Cause { get; }

    /// <summary>Gets the optional application payload.</summary>
    public object? Payload { get; }
}
