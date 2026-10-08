using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core;

/// <summary>Builds a MuLang language profile.</summary>
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
    private ConstantFoldingFeature constantFolding;
    private ObjectLiteralSyntax objectLiteralSyntax;
    private ConditionSemantics conditionSemantics;
    private ShadowingPolicy shadowing;

    /// <summary>Initializes a new instance of the <see cref="LanguageProfileBuilder" /> class.</summary>
    public LanguageProfileBuilder()
        : this(LanguageProfiles.Latest) { }

    /// <summary>Initializes a new instance of the <see cref="LanguageProfileBuilder" /> class.</summary>
    /// <param name="profile">The language profile used for compilation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile" /> is <c>null</c>.</exception>
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
        constantFolding = profile.ConstantFolding;
        objectLiteralSyntax = profile.ObjectLiteralSyntax;
        conditionSemantics = profile.ConditionSemantics;
        shadowing = profile.Shadowing;
    }

    /// <summary>Sets the language version setting.</summary>
    /// <param name="languageVersion">The language version.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="languageVersion" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="languageVersion" /> is not supported.</exception>
    public LanguageProfileBuilder WithLanguageVersion(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] LanguageVersion languageVersion
    )
    {
        LanguageProfile.ValidateLanguageVersion(
            languageVersion,
            nameof(languageVersion)
        );
        this.languageVersion = languageVersion;

        return this;
    }

    /// <summary>Sets the user-defined functions feature setting.</summary>
    /// <param name="userDefinedFunctions">The user-defined functions feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userDefinedFunctions" /> is not defined.</exception>
    public LanguageProfileBuilder WithUserDefinedFunctions(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] UserDefinedFunctionsFeature userDefinedFunctions
    )
    {
        LanguageProfile.ValidateDefined(
            userDefinedFunctions,
            nameof(userDefinedFunctions)
        );
        this.userDefinedFunctions = userDefinedFunctions;

        return this;
    }

    /// <summary>Sets the recursion setting.</summary>
    /// <param name="recursion">The recursion feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="recursion" /> is not defined.</exception>
    public LanguageProfileBuilder WithRecursion(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] RecursionFeature recursion
    )
    {
        LanguageProfile.ValidateDefined(recursion, nameof(recursion));
        this.recursion = recursion;

        return this;
    }

    /// <summary>Sets the loops setting.</summary>
    /// <param name="loops">The loop features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="loops" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder WithLoops(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] LoopFeatures loops
    )
    {
        ValidateLoops(loops);
        this.loops = loops;

        return this;
    }

    /// <summary>Enables the specified loops.</summary>
    /// <param name="loops">The loop features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="loops" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder EnableLoops(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] LoopFeatures loops
    )
    {
        ValidateLoops(loops);
        this.loops |= loops;

        return this;
    }

    /// <summary>Disables the specified loops.</summary>
    /// <param name="loops">The loop features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="loops" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder DisableLoops(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] LoopFeatures loops
    )
    {
        ValidateLoops(loops);
        this.loops &= ~loops;

        return this;
    }

    /// <summary>Sets the provider function calls setting.</summary>
    /// <param name="providerFunctionCalls">The provider function calls feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="providerFunctionCalls" /> is not defined.</exception>
    public LanguageProfileBuilder WithProviderFunctionCalls(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ProviderFunctionCallsFeature providerFunctionCalls
    )
    {
        LanguageProfile.ValidateDefined(
            providerFunctionCalls,
            nameof(providerFunctionCalls)
        );
        this.providerFunctionCalls = providerFunctionCalls;

        return this;
    }

    /// <summary>Sets the open objects setting.</summary>
    /// <param name="openObjects">The open objects feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="openObjects" /> is not defined.</exception>
    public LanguageProfileBuilder WithOpenObjects(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] OpenObjectsFeature openObjects
    )
    {
        LanguageProfile.ValidateDefined(openObjects, nameof(openObjects));
        this.openObjects = openObjects;

        return this;
    }

    /// <summary>Sets the mutations setting.</summary>
    /// <param name="mutations">The mutation features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mutations" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder WithMutations(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] MutationFeatures mutations
    )
    {
        ValidateMutations(mutations);
        this.mutations = mutations;

        return this;
    }

    /// <summary>Enables the specified mutations.</summary>
    /// <param name="mutations">The mutation features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mutations" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder EnableMutations(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] MutationFeatures mutations
    )
    {
        ValidateMutations(mutations);
        this.mutations |= mutations;

        return this;
    }

    /// <summary>Disables the specified mutations.</summary>
    /// <param name="mutations">The mutation features.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mutations" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder DisableMutations(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] MutationFeatures mutations
    )
    {
        ValidateMutations(mutations);
        this.mutations &= ~mutations;

        return this;
    }

    /// <summary>Sets the multi-level loop control feature setting.</summary>
    /// <param name="multiLevelLoopControl">The multi-level loop control feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="multiLevelLoopControl" /> is not defined.</exception>
    public LanguageProfileBuilder WithMultiLevelLoopControl(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] MultiLevelLoopControlFeature multiLevelLoopControl
    )
    {
        LanguageProfile.ValidateDefined(
            multiLevelLoopControl,
            nameof(multiLevelLoopControl)
        );
        this.multiLevelLoopControl = multiLevelLoopControl;

        return this;
    }

    /// <summary>Sets the trailing commas setting.</summary>
    /// <param name="trailingCommas">The trailing commas feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="trailingCommas" /> is not defined.</exception>
    public LanguageProfileBuilder WithTrailingCommas(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] TrailingCommasFeature trailingCommas
    )
    {
        LanguageProfile.ValidateDefined(trailingCommas, nameof(trailingCommas));
        this.trailingCommas = trailingCommas;

        return this;
    }

    /// <summary>Sets the compile-time constant folding feature setting.</summary>
    /// <param name="constantFolding">The compile-time constant folding feature setting.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="constantFolding" /> is not defined.</exception>
    public LanguageProfileBuilder WithConstantFolding(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ConstantFoldingFeature constantFolding
    )
    {
        LanguageProfile.ValidateDefined(constantFolding, nameof(constantFolding));
        this.constantFolding = constantFolding;

        return this;
    }

    /// <summary>Sets the object-literal property grammar.</summary>
    /// <param name="objectLiteralSyntax">The object-literal property grammar.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="objectLiteralSyntax" /> is not defined.</exception>
    public LanguageProfileBuilder WithObjectLiteralSyntax(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ObjectLiteralSyntax objectLiteralSyntax
    )
    {
        LanguageProfile.ValidateDefined(
            objectLiteralSyntax,
            nameof(objectLiteralSyntax)
        );
        this.objectLiteralSyntax = objectLiteralSyntax;

        return this;
    }

    /// <summary>Sets the condition semantics setting.</summary>
    /// <param name="conditionSemantics">The condition semantics.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="conditionSemantics" /> is not defined.</exception>
    public LanguageProfileBuilder WithConditionSemantics(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ConditionSemantics conditionSemantics
    )
    {
        LanguageProfile.ValidateConditionSemantics(
            conditionSemantics,
            nameof(conditionSemantics)
        );
        this.conditionSemantics = conditionSemantics;

        return this;
    }

    /// <summary>Sets the shadowing setting.</summary>
    /// <param name="shadowing">The shadowing policy.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shadowing" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder WithShadowing(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ShadowingPolicy shadowing
    )
    {
        ValidateShadowing(shadowing);
        this.shadowing = shadowing;

        return this;
    }

    /// <summary>Enables the specified shadowing.</summary>
    /// <param name="shadowing">The shadowing policy.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shadowing" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder EnableShadowing(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ShadowingPolicy shadowing
    )
    {
        ValidateShadowing(shadowing);
        this.shadowing |= shadowing;

        return this;
    }

    /// <summary>Disables the specified shadowing.</summary>
    /// <param name="shadowing">The shadowing policy.</param>
    /// <returns>The same <see cref="LanguageProfileBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="shadowing" /> contains unsupported flags.</exception>
    public LanguageProfileBuilder DisableShadowing(
        [SuppressMessage("ReSharper", "ParameterHidesMember")] ShadowingPolicy shadowing
    )
    {
        ValidateShadowing(shadowing);
        this.shadowing &= ~shadowing;

        return this;
    }

    /// <summary>Creates an immutable language profile from the current settings.</summary>
    /// <returns>The immutable language profile.</returns>
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
            constantFolding,
            objectLiteralSyntax,
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
