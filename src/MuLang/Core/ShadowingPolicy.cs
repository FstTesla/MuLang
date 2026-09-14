namespace MuLang.Core;

[Flags]
public enum ShadowingPolicy
{
    None = 0,
    NestedScopes = 1,
    Globals = 2,
}
