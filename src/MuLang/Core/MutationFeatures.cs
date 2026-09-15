namespace MuLang.Core;

/// <summary>Specifies the mutation operations enabled in a language profile.</summary>
[Flags]
public enum MutationFeatures
{
    /// <summary>Enables no mutation operations.</summary>
    None = 0,
    /// <summary>Enables assignment to object properties.</summary>
    ObjectProperties = 1,
    /// <summary>Enables assignment to array elements.</summary>
    ArrayElements = 2,
    /// <summary>Enables removal of object properties.</summary>
    PropertyRemoval = 4,
}
