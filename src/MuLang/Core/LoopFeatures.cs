namespace MuLang.Core;

/// <summary>Specifies the loop constructs enabled in a language profile.</summary>
[Flags]
public enum LoopFeatures
{
    /// <summary>Enables no loop constructs.</summary>
    None = 0,
    /// <summary>Enables <c>while</c> loops.</summary>
    While = 1 << 0,
    /// <summary>Enables <c>for</c> loops.</summary>
    For = 1 << 1,
}
