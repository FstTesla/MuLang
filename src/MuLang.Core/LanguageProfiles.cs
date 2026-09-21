namespace MuLang.Core;

/// <summary>Provides predefined MuLang language profiles.</summary>
public static class LanguageProfiles
{
    /// <summary>Gets the default profile for MuLang language version 1.</summary>
    public static LanguageProfile Version1 { get; } = new (
        LanguageVersion.Version1,
        UserDefinedFunctionsFeature.Enabled,
        RecursionFeature.Enabled,
        LoopFeatures.While | LoopFeatures.For,
        ProviderFunctionCallsFeature.Enabled,
        OpenObjectsFeature.Enabled,
        MutationFeatures.ObjectProperties |
        MutationFeatures.ArrayElements |
        MutationFeatures.PropertyRemoval,
        MultiLevelLoopControlFeature.Enabled,
        TrailingCommasFeature.Enabled,
        ConditionSemantics.StrictBoolean,
        ShadowingPolicy.None
    );
}
