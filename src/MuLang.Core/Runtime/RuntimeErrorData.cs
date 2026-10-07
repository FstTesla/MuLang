namespace MuLang.Core.Runtime;

/// <summary>Represents the optional application payload of a runtime error.</summary>
/// <param name="IsPresent">Whether the payload property is present.</param>
/// <param name="Value">The payload value when present.</param>
public readonly record struct RuntimeErrorData(
    bool IsPresent,
    object? Value
)
{
    /// <summary>Gets an absent payload.</summary>
    public static RuntimeErrorData Absent { get; } = new (false, null);

    /// <summary>Creates a present payload.</summary>
    /// <param name="value">The payload value.</param>
    /// <returns>The present payload.</returns>
    public static RuntimeErrorData Present(object? value)
    {
        return new RuntimeErrorData(true, value);
    }
}
