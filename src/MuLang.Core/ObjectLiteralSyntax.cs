namespace MuLang.Core;

/// <summary>Specifies the object-literal property grammar.</summary>
public enum ObjectLiteralSyntax
{
    /// <summary>Uses the legacy initializer-only property grammar.</summary>
    Legacy = 0,

    /// <summary>Uses the full declaration and initializer property grammar.</summary>
    Full = 1,
}
