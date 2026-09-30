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
    {
        if (string.IsNullOrWhiteSpace(functionId))
        {
            throw new ArgumentException(
                "Function identifier cannot be null or whitespace.",
                nameof(functionId)
            );
        }

        FunctionId = functionId;
        CallSpan = callSpan;
    }

    /// <summary>Gets the portable function identifier.</summary>
    public string FunctionId { get; }

    /// <summary>Gets the source span that entered the function.</summary>
    public TextSpan CallSpan { get; }
}
