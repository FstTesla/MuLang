namespace MuLang.Core;

public sealed class LanguageProfileBuilder
{
    private LanguageVersion languageVersion;
    private UserDefinedFunctionsFeature userDefinedFunctions;
    private RecursionFeature recursion;
    private LoopFeatures loops;
    private ProviderFunctionCallsFeature providerFunctionCalls;
    private OpenObjectsFeature openObjects;
    private MutationFeatures mutations;
    private MultiLevelLoopControlFeature multiLevelLoopControl;
    private TrailingCommasFeature trailingCommas;
    private ConditionSemantics conditionSemantics;
    private ShadowingPolicy shadowing;

    public LanguageProfileBuilder()
        : this(LanguageProfiles.Version1) { }

    public LanguageProfileBuilder(LanguageProfile profile)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        languageVersion = profile.LanguageVersion;
        userDefinedFunctions = profile.UserDefinedFunctions;
        recursion = profile.Recursion;
        loops = profile.Loops;
        providerFunctionCalls = profile.ProviderFunctionCalls;
        openObjects = profile.OpenObjects;
        mutations = profile.Mutations;
        multiLevelLoopControl = profile.MultiLevelLoopControl;
        trailingCommas = profile.TrailingCommas;
        conditionSemantics = profile.ConditionSemantics;
        shadowing = profile.Shadowing;
    }

    public LanguageProfileBuilder WithLanguageVersion(
        LanguageVersion languageVersion
    )
    {
        LanguageProfile.ValidateLanguageVersion(
            languageVersion,
            nameof(languageVersion)
        );
        this.languageVersion = languageVersion;

        return this;
    }

    public LanguageProfileBuilder WithUserDefinedFunctions(
        UserDefinedFunctionsFeature userDefinedFunctions
    )
    {
        LanguageProfile.ValidateDefined(
            userDefinedFunctions,
            nameof(userDefinedFunctions)
        );
        this.userDefinedFunctions = userDefinedFunctions;

        return this;
    }

    public LanguageProfileBuilder WithRecursion(RecursionFeature recursion)
    {
        LanguageProfile.ValidateDefined(recursion, nameof(recursion));
        this.recursion = recursion;

        return this;
    }

    public LanguageProfileBuilder WithLoops(LoopFeatures loops)
    {
        ValidateLoops(loops);
        this.loops = loops;

        return this;
    }

    public LanguageProfileBuilder EnableLoops(LoopFeatures loops)
    {
        ValidateLoops(loops);
        this.loops |= loops;

        return this;
    }

    public LanguageProfileBuilder DisableLoops(LoopFeatures loops)
    {
        ValidateLoops(loops);
        this.loops &= ~loops;

        return this;
    }

    public LanguageProfileBuilder WithProviderFunctionCalls(
        ProviderFunctionCallsFeature providerFunctionCalls
    )
    {
        LanguageProfile.ValidateDefined(
            providerFunctionCalls,
            nameof(providerFunctionCalls)
        );
        this.providerFunctionCalls = providerFunctionCalls;

        return this;
    }

    public LanguageProfileBuilder WithOpenObjects(OpenObjectsFeature openObjects)
    {
        LanguageProfile.ValidateDefined(openObjects, nameof(openObjects));
        this.openObjects = openObjects;

        return this;
    }

    public LanguageProfileBuilder WithMutations(MutationFeatures mutations)
    {
        ValidateMutations(mutations);
        this.mutations = mutations;

        return this;
    }

    public LanguageProfileBuilder EnableMutations(MutationFeatures mutations)
    {
        ValidateMutations(mutations);
        this.mutations |= mutations;

        return this;
    }

    public LanguageProfileBuilder DisableMutations(MutationFeatures mutations)
    {
        ValidateMutations(mutations);
        this.mutations &= ~mutations;

        return this;
    }

    public LanguageProfileBuilder WithMultiLevelLoopControl(
        MultiLevelLoopControlFeature multiLevelLoopControl
    )
    {
        LanguageProfile.ValidateDefined(
            multiLevelLoopControl,
            nameof(multiLevelLoopControl)
        );
        this.multiLevelLoopControl = multiLevelLoopControl;

        return this;
    }

    public LanguageProfileBuilder WithTrailingCommas(
        TrailingCommasFeature trailingCommas
    )
    {
        LanguageProfile.ValidateDefined(trailingCommas, nameof(trailingCommas));
        this.trailingCommas = trailingCommas;

        return this;
    }

    public LanguageProfileBuilder WithConditionSemantics(
        ConditionSemantics conditionSemantics
    )
    {
        LanguageProfile.ValidateConditionSemantics(
            conditionSemantics,
            nameof(conditionSemantics)
        );
        this.conditionSemantics = conditionSemantics;

        return this;
    }

    public LanguageProfileBuilder WithShadowing(ShadowingPolicy shadowing)
    {
        ValidateShadowing(shadowing);
        this.shadowing = shadowing;

        return this;
    }

    public LanguageProfileBuilder EnableShadowing(ShadowingPolicy shadowing)
    {
        ValidateShadowing(shadowing);
        this.shadowing |= shadowing;

        return this;
    }

    public LanguageProfileBuilder DisableShadowing(ShadowingPolicy shadowing)
    {
        ValidateShadowing(shadowing);
        this.shadowing &= ~shadowing;

        return this;
    }

    public LanguageProfile Build()
    {
        return new LanguageProfile(
            languageVersion,
            userDefinedFunctions,
            recursion,
            loops,
            providerFunctionCalls,
            openObjects,
            mutations,
            multiLevelLoopControl,
            trailingCommas,
            conditionSemantics,
            shadowing
        );
    }

    private static void ValidateLoops(LoopFeatures loops)
    {
        LanguageProfile.ValidateFlags(
            loops,
            LoopFeatures.While | LoopFeatures.For,
            nameof(loops)
        );
    }

    private static void ValidateMutations(MutationFeatures mutations)
    {
        LanguageProfile.ValidateFlags(
            mutations,
            MutationFeatures.ObjectProperties |
            MutationFeatures.ArrayElements |
            MutationFeatures.PropertyRemoval,
            nameof(mutations)
        );
    }

    private static void ValidateShadowing(ShadowingPolicy shadowing)
    {
        LanguageProfile.ValidateFlags(
            shadowing,
            ShadowingPolicy.NestedScopes | ShadowingPolicy.Globals,
            nameof(shadowing)
        );
    }
}
