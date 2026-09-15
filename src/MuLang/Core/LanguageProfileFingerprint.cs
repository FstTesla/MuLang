namespace MuLang.Core;

/// <summary>Represents a stable fingerprint of a MuLang language profile.</summary>
public sealed record LanguageProfileFingerprint
{
    /// <summary>Initializes a new instance of the <see cref="LanguageProfileFingerprint" /> class.</summary>
    /// <param name="value">The fingerprint value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is null, empty, or whitespace.</exception>
    public LanguageProfileFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Language profile fingerprint cannot be null or whitespace.",
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
