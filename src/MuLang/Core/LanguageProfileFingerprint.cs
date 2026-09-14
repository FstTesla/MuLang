namespace MuLang.Core;

public sealed record LanguageProfileFingerprint
{
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

    public string Value { get; }

    public override string ToString() => Value;
}
