namespace MuLang.Core.Environment;

/// <summary>Represents a stable fingerprint of a MuLang environment schema.</summary>
public sealed record EnvironmentFingerprint
{
    /// <summary>Initializes a new instance of the <see cref="EnvironmentFingerprint" /> class.</summary>
    /// <param name="value">The fingerprint value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is null, empty, or whitespace.</exception>
    public EnvironmentFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Environment fingerprint cannot be null or whitespace.",
                nameof(value)
            );
        }

        Value = value;
    }

    /// <summary>Gets the fingerprint value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
