namespace MuLang.Core;

public sealed record LanguageProfile
{
    public LanguageProfile(
        LanguageVersion languageVersion,
        UserDefinedFunctionsFeature userDefinedFunctions,
        RecursionFeature recursion,
        LoopFeatures loops,
        ProviderFunctionCallsFeature providerFunctionCalls,
        OpenObjectsFeature openObjects,
        MutationFeatures mutations,
        OptionalAccessFeature optionalAccess,
        MultiLevelLoopControlFeature multiLevelLoopControl,
        TrailingCommasFeature trailingCommas,
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
        ValidateDefined(optionalAccess, nameof(optionalAccess));
        ValidateDefined(multiLevelLoopControl, nameof(multiLevelLoopControl));
        ValidateDefined(trailingCommas, nameof(trailingCommas));
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
        OptionalAccess = optionalAccess;
        MultiLevelLoopControl = multiLevelLoopControl;
        TrailingCommas = trailingCommas;
        ConditionSemantics = conditionSemantics;
        Shadowing = shadowing;
        Fingerprint = LanguageProfileFingerprintFactory.Create(this);
    }

    public LanguageVersion LanguageVersion { get; }

    public UserDefinedFunctionsFeature UserDefinedFunctions { get; }

    public RecursionFeature Recursion { get; }

    public LoopFeatures Loops { get; }

    public ProviderFunctionCallsFeature ProviderFunctionCalls { get; }

    public OpenObjectsFeature OpenObjects { get; }

    public MutationFeatures Mutations { get; }

    public OptionalAccessFeature OptionalAccess { get; }

    public MultiLevelLoopControlFeature MultiLevelLoopControl { get; }

    public TrailingCommasFeature TrailingCommas { get; }

    public ConditionSemantics ConditionSemantics { get; }

    public ShadowingPolicy Shadowing { get; }

    public LanguageProfileFingerprint Fingerprint { get; }

    internal static void ValidateLanguageVersion(
        LanguageVersion value,
        string parameterName
    )
    {
        ValidateDefined(value, parameterName);

        if (value != LanguageVersion.Version1)
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
