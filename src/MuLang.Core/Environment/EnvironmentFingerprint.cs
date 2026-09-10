namespace MuLang.Core.Environment;

public sealed record EnvironmentFingerprint
{
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

    public string Value { get; }

    public override string ToString() => Value;
}
