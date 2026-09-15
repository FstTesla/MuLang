namespace MuLang.Core;

/// <summary>Specifies whether loop control statements may target enclosing loops.</summary>
public enum MultiLevelLoopControlFeature
{
    /// <summary>Disables the feature.</summary>
    Disabled = 0,
    /// <summary>Enables the feature.</summary>
    Enabled = 1,
}
