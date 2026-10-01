namespace MuLang.StandardLibrary;

/// <summary>Specifies runtime capabilities required by a standard-library module.</summary>
[Flags]
public enum StandardLibraryCapability
{
    /// <summary>Indicates that the module is deterministic.</summary>
    Deterministic = 0,

    /// <summary>Indicates that the module requires randomness.</summary>
    Randomness = 1 << 0,

    /// <summary>Indicates that the module requires a clock.</summary>
    Clock = 1 << 1,

    /// <summary>Indicates that the module requires randomness and a clock.</summary>
    RandomnessAndClock = Randomness | Clock,
}
