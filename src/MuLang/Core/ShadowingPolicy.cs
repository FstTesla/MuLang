namespace MuLang.Core;

/// <summary>Specifies where declarations may shadow existing symbols.</summary>
[Flags]
public enum ShadowingPolicy
{
    /// <summary>Allows no symbol shadowing.</summary>
    None = 0,
    /// <summary>Allows declarations to shadow symbols from enclosing local scopes.</summary>
    NestedScopes = 1,
    /// <summary>Allows local declarations to shadow global symbols.</summary>
    Globals = 2,
}
