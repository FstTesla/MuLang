namespace MuLang.Core;

/// <summary>Represents the language features and semantics enabled for MuLang compilation.</summary>
public sealed record LanguageProfile
{
    /// <summary>Initializes a new instance of the <see cref="LanguageProfile" /> class.</summary>
    /// <param name="languageVersion">The language version.</param>
    /// <param name="userDefinedFunctions">The user-defined functions feature setting.</param>
    /// <param name="recursion">The recursion feature setting.</param>
    /// <param name="loops">The loop features.</param>
    /// <param name="providerFunctionCalls">The provider function calls feature setting.</param>
    /// <param name="openObjects">The open objects feature setting.</param>
    /// <param name="mutations">The mutation features.</param>
    /// <param name="multiLevelLoopControl">The multi-level loop control feature setting.</param>
    /// <param name="trailingCommas">The trailing commas feature setting.</param>
    /// <param name="constantFolding">The compile-time constant folding feature setting.</param>
    /// <param name="conditionSemantics">The condition semantics.</param>
    /// <param name="shadowing">The shadowing policy.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a feature setting or flags value is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="languageVersion" /> is not supported.</exception>
    internal LanguageProfile(
        LanguageVersion languageVersion,
        UserDefinedFunctionsFeature userDefinedFunctions,
        RecursionFeature recursion,
        LoopFeatures loops,
        ProviderFunctionCallsFeature providerFunctionCalls,
        OpenObjectsFeature openObjects,
        MutationFeatures mutations,
        MultiLevelLoopControlFeature multiLevelLoopControl,
        TrailingCommasFeature trailingCommas,
        ConstantFoldingFeature constantFolding,
        ConditionSemantics conditionSemantics,
        ShadowingPolicy shadowing
    )
    {
        ValidateLanguageVersion(languageVersion, nameof(languageVersion));
        ValidateDefined(userDefinedFunctions, nameof(userDefinedFunctions));
        ValidateDefined(recursion, nameof(recursion));
        ValidateFlags(loops, LoopFeatures.While | LoopFeatures.For, nameof(loops));
        ValidateDefined(providerFunctionCalls, nameof(providerFunctionCalls));
        ValidateDefined(openObjects, nameof(openObjects));
        ValidateFlags(
            mutations,
            MutationFeatures.ObjectProperties |
            MutationFeatures.ArrayElements |
            MutationFeatures.PropertyRemoval,
            nameof(mutations)
        );
        ValidateDefined(multiLevelLoopControl, nameof(multiLevelLoopControl));
        ValidateDefined(trailingCommas, nameof(trailingCommas));
        ValidateDefined(constantFolding, nameof(constantFolding));
        ValidateConditionSemantics(conditionSemantics, nameof(conditionSemantics));
        ValidateFlags(
            shadowing,
            ShadowingPolicy.NestedScopes | ShadowingPolicy.Globals,
            nameof(shadowing)
        );

        LanguageVersion = languageVersion;
        UserDefinedFunctions = userDefinedFunctions;
        Recursion = recursion;
        Loops = loops;
        ProviderFunctionCalls = providerFunctionCalls;
        OpenObjects = openObjects;
        Mutations = mutations;
        MultiLevelLoopControl = multiLevelLoopControl;
        TrailingCommas = trailingCommas;
        ConstantFolding = constantFolding;
        ConditionSemantics = conditionSemantics;
        Shadowing = shadowing;
        Fingerprint = LanguageProfileFingerprintFactory.Create(this);
    }

    /// <summary>Gets the language version.</summary>
    public LanguageVersion LanguageVersion { get; }

    /// <summary>Gets the user-defined functions feature setting.</summary>
    public UserDefinedFunctionsFeature UserDefinedFunctions { get; }

    /// <summary>Gets the recursion feature setting.</summary>
    public RecursionFeature Recursion { get; }

    /// <summary>Gets the enabled loop features.</summary>
    public LoopFeatures Loops { get; }

    /// <summary>Gets the provider function calls feature setting.</summary>
    public ProviderFunctionCallsFeature ProviderFunctionCalls { get; }

    /// <summary>Gets the open objects feature setting.</summary>
    public OpenObjectsFeature OpenObjects { get; }

    /// <summary>Gets the enabled mutation features.</summary>
    public MutationFeatures Mutations { get; }

    /// <summary>Gets the multi-level loop control feature setting.</summary>
    public MultiLevelLoopControlFeature MultiLevelLoopControl { get; }

    /// <summary>Gets the trailing commas feature setting.</summary>
    public TrailingCommasFeature TrailingCommas { get; }

    /// <summary>Gets the compile-time constant folding feature setting.</summary>
    public ConstantFoldingFeature ConstantFolding { get; }

    /// <summary>Gets the condition semantics.</summary>
    public ConditionSemantics ConditionSemantics { get; }

    /// <summary>Gets the shadowing policy.</summary>
    public ShadowingPolicy Shadowing { get; }

    /// <summary>Gets the stable fingerprint of the profile.</summary>
    public LanguageProfileFingerprint Fingerprint { get; }

    internal static void ValidateLanguageVersion(
        LanguageVersion value,
        string parameterName
    )
    {
        ValidateDefined(value, parameterName);

        if (value is not LanguageVersion.Version1 and not LanguageVersion.Version1_1)
        {
            throw new ArgumentException(
                $"Language version '{value}' is not supported.",
                parameterName
            );
        }
    }

    internal static void ValidateConditionSemantics(
        ConditionSemantics value,
        string parameterName
    )
    {
        ValidateDefined(value, parameterName);
    }

    internal static void ValidateDefined<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void ValidateFlags<T>(
        T value,
        T allowed,
        string parameterName
    )
        where T : struct, Enum
    {
        ulong numericValue = unchecked((ulong)Convert.ToInt64(value));
        ulong allowedValue = unchecked((ulong)Convert.ToInt64(allowed));

        if ((numericValue & ~allowedValue) != 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
