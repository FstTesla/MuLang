namespace MuLang.Core;

[Flags]
public enum MutationFeatures
{
    None = 0,
    ObjectProperties = 1,
    ArrayElements = 2,
    PropertyRemoval = 4,
}
