namespace MuLang.Core;

/// <summary>Provides predefined MuLang language profiles.</summary>
public static class LanguageProfiles
{
    /// <summary>Gets the standard profile for the latest supported MuLang language version.</summary>
    public static LanguageProfile Latest => Version1_2;

    /// <summary>Gets the standard profile for MuLang language version 1.</summary>
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
        ConstantFoldingFeature.Enabled,
        ExceptionHandlingFeature.Disabled,
        ObjectLiteralSyntax.Legacy,
        ConditionSemantics.StrictBoolean,
        ShadowingPolicy.None
    );

    /// <summary>Gets the standard profile for MuLang language version 1.1.</summary>
    public static LanguageProfile Version1_1 { get; } = new (
        LanguageVersion.Version1_1,
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
        ConstantFoldingFeature.Enabled,
        ExceptionHandlingFeature.Disabled,
        ObjectLiteralSyntax.Legacy,
        ConditionSemantics.StrictBoolean,
        ShadowingPolicy.None
    );

    /// <summary>Gets the standard profile for MuLang language version 1.2.</summary>
    public static LanguageProfile Version1_2 { get; } = new (
        LanguageVersion.Version1_2,
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
        ConstantFoldingFeature.Enabled,
        ExceptionHandlingFeature.Enabled,
        ObjectLiteralSyntax.Full,
        ConditionSemantics.StrictBoolean,
        ShadowingPolicy.None
    );
}
