namespace MuLang.Core;

public static class LanguageProfiles
{
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
