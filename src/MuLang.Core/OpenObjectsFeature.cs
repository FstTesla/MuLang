namespace MuLang.Core;

/// <summary>Specifies the available open-object capabilities.</summary>
public enum OpenObjectsFeature
{
    /// <summary>Disables open objects and dynamic property tests.</summary>
    Disabled = 0,

    /// <summary>Enables dynamic property tests without enabling open objects.</summary>
    PropertyExistenceOnly = 1,

    /// <summary>Enables all open-object capabilities.</summary>
    Enabled = 2,
}
