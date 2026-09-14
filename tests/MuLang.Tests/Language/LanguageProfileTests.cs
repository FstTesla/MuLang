using MuLang.Core;

namespace MuLang.Tests.Language;

public sealed class LanguageProfileTests
{
    [Test]
    public void StandardProfilePreservesVersionOneBehavior()
    {
        LanguageProfile profile = LanguageProfiles.Version1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.LanguageVersion, Is.EqualTo(LanguageVersion.Version1));
            Assert.That(
                profile.UserDefinedFunctions,
                Is.EqualTo(UserDefinedFunctionsFeature.Enabled)
            );
            Assert.That(profile.Recursion, Is.EqualTo(RecursionFeature.Enabled));
            Assert.That(
                profile.Loops,
                Is.EqualTo(LoopFeatures.While | LoopFeatures.For)
            );
            Assert.That(
                profile.ProviderFunctionCalls,
                Is.EqualTo(ProviderFunctionCallsFeature.Enabled)
            );
            Assert.That(profile.OpenObjects, Is.EqualTo(OpenObjectsFeature.Enabled));
            Assert.That(
                profile.Mutations,
                Is.EqualTo(
                    MutationFeatures.ObjectProperties |
                    MutationFeatures.ArrayElements |
                    MutationFeatures.PropertyRemoval
                )
            );
            Assert.That(
                profile.OptionalAccess,
                Is.EqualTo(OptionalAccessFeature.Enabled)
            );
            Assert.That(
                profile.MultiLevelLoopControl,
                Is.EqualTo(MultiLevelLoopControlFeature.Enabled)
            );
            Assert.That(
                profile.TrailingCommas,
                Is.EqualTo(TrailingCommasFeature.Enabled)
            );
            Assert.That(
                profile.ConditionSemantics,
                Is.EqualTo(ConditionSemantics.StrictBoolean)
            );
            Assert.That(profile.Shadowing, Is.EqualTo(ShadowingPolicy.None));
        }
    }

    [Test]
    public void CombinableEnumsHaveStableValues()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)LoopFeatures.None, Is.Zero);
            Assert.That((int)LoopFeatures.While, Is.EqualTo(1));
            Assert.That((int)LoopFeatures.For, Is.EqualTo(2));
            Assert.That((int)MutationFeatures.None, Is.Zero);
            Assert.That((int)MutationFeatures.ObjectProperties, Is.EqualTo(1));
            Assert.That((int)MutationFeatures.ArrayElements, Is.EqualTo(2));
            Assert.That((int)MutationFeatures.PropertyRemoval, Is.EqualTo(4));
            Assert.That((int)ShadowingPolicy.None, Is.Zero);
            Assert.That((int)ShadowingPolicy.NestedScopes, Is.EqualTo(1));
            Assert.That((int)ShadowingPolicy.Globals, Is.EqualTo(2));
        }
    }

    [Test]
    public void OrdinaryEnumsHaveStableValues()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)UserDefinedFunctionsFeature.Disabled, Is.Zero);
            Assert.That((int)UserDefinedFunctionsFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)RecursionFeature.Disabled, Is.Zero);
            Assert.That((int)RecursionFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)ProviderFunctionCallsFeature.Disabled, Is.Zero);
            Assert.That((int)ProviderFunctionCallsFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)OpenObjectsFeature.Disabled, Is.Zero);
            Assert.That((int)OpenObjectsFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)OptionalAccessFeature.Disabled, Is.Zero);
            Assert.That((int)OptionalAccessFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)MultiLevelLoopControlFeature.Disabled, Is.Zero);
            Assert.That((int)MultiLevelLoopControlFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)TrailingCommasFeature.Disabled, Is.Zero);
            Assert.That((int)TrailingCommasFeature.Enabled, Is.EqualTo(1));
            Assert.That((int)ConditionSemantics.StrictBoolean, Is.Zero);
            Assert.That((int)ConditionSemantics.Truthiness, Is.EqualTo(1));
        }
    }

    [Test]
    public void ProfileHasOnePublicConstructorAndReadOnlyProperties()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeof(LanguageProfile).GetConstructors(), Has.Length.EqualTo(1));
            Assert.That(
                typeof(LanguageProfile)
                    .GetProperties()
                    .Select(static property => property.SetMethod),
                Has.All.Null
            );
        }
    }

    [Test]
    public void DefaultBuilderCreatesStandardProfile()
    {
        LanguageProfile profile = new LanguageProfileBuilder().Build();

        Assert.That(profile, Is.EqualTo(LanguageProfiles.Version1));
    }

    [Test]
    public void BuilderCanStartFromExistingProfile()
    {
        LanguageProfile source = new LanguageProfileBuilder()
            .WithUserDefinedFunctions(UserDefinedFunctionsFeature.Disabled)
            .WithLoops(LoopFeatures.While)
            .WithMutations(MutationFeatures.ArrayElements)
            .WithShadowing(ShadowingPolicy.Globals)
            .Build();
        LanguageProfile copy = new LanguageProfileBuilder(source).Build();

        Assert.That(copy, Is.EqualTo(source));
    }

    [Test]
    public void BuilderSupportsWholeAndGranularCombinableValues()
    {
        LanguageProfile profile = new LanguageProfileBuilder()
            .WithLoops(LoopFeatures.None)
            .EnableLoops(LoopFeatures.While | LoopFeatures.For)
            .DisableLoops(LoopFeatures.While)
            .WithMutations(MutationFeatures.None)
            .EnableMutations(
                MutationFeatures.ObjectProperties |
                MutationFeatures.PropertyRemoval
            )
            .DisableMutations(MutationFeatures.ObjectProperties)
            .WithShadowing(ShadowingPolicy.None)
            .EnableShadowing(
                ShadowingPolicy.NestedScopes |
                ShadowingPolicy.Globals
            )
            .DisableShadowing(ShadowingPolicy.Globals)
            .Build();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.Loops, Is.EqualTo(LoopFeatures.For));
            Assert.That(
                profile.Mutations,
                Is.EqualTo(MutationFeatures.PropertyRemoval)
            );
            Assert.That(
                profile.Shadowing,
                Is.EqualTo(ShadowingPolicy.NestedScopes)
            );
        }
    }

    [Test]
    public void BuilderBuildCreatesIndependentSnapshots()
    {
        LanguageProfileBuilder builder = new ();
        LanguageProfile first = builder.Build();
        LanguageProfile second = builder
            .WithRecursion(RecursionFeature.Disabled)
            .Build();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Recursion, Is.EqualTo(RecursionFeature.Enabled));
            Assert.That(second.Recursion, Is.EqualTo(RecursionFeature.Disabled));
        }
    }

    [TestCaseSource(nameof(InvalidBuilderConfigurations))]
    public void BuilderValidatesValuesImmediately(Action<LanguageProfileBuilder> configure)
    {
        LanguageProfileBuilder builder = new ();

        Assert.That(() => configure(builder), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void EquivalentProfilesHaveEqualDeterministicFingerprints()
    {
        LanguageProfile first = TestLanguageProfileFactory.Create();
        LanguageProfile second = TestLanguageProfileFactory.Create();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.Fingerprint.Value, Has.Length.EqualTo(64));
            Assert.That(
                first.Fingerprint.Value,
                Is.EqualTo(
                    "ea845d7090c3e6eb09c86fe7c811f6b401e98c69f3b0e04c8f2551a0c1c97a85"
                )
            );
        }
    }

    [TestCase("functions")]
    [TestCase("recursion")]
    [TestCase("loops")]
    [TestCase("provider")]
    [TestCase("openObjects")]
    [TestCase("mutations")]
    [TestCase("optionalAccess")]
    [TestCase("loopControl")]
    [TestCase("trailingCommas")]
    [TestCase("shadowing")]
    public void FingerprintChangesForEveryConfigurableSupportedConcern(string concern)
    {
        LanguageProfile changed = concern switch
        {
            "functions" => TestLanguageProfileFactory.Create(
                userDefinedFunctions: UserDefinedFunctionsFeature.Disabled
            ),
            "recursion" => TestLanguageProfileFactory.Create(
                recursion: RecursionFeature.Disabled
            ),
            "loops" => TestLanguageProfileFactory.Create(loops: LoopFeatures.For),
            "provider" => TestLanguageProfileFactory.Create(
                providerFunctionCalls: ProviderFunctionCallsFeature.Disabled
            ),
            "openObjects" => TestLanguageProfileFactory.Create(
                openObjects: OpenObjectsFeature.Disabled
            ),
            "mutations" => TestLanguageProfileFactory.Create(
                mutations: MutationFeatures.ArrayElements
            ),
            "optionalAccess" => TestLanguageProfileFactory.Create(
                optionalAccess: OptionalAccessFeature.Disabled
            ),
            "loopControl" => TestLanguageProfileFactory.Create(
                multiLevelLoopControl: MultiLevelLoopControlFeature.Disabled
            ),
            "trailingCommas" => TestLanguageProfileFactory.Create(
                trailingCommas: TrailingCommasFeature.Disabled
            ),
            "shadowing" => TestLanguageProfileFactory.Create(
                shadowing: ShadowingPolicy.Globals
            ),
            _ => throw new AssertionException($"Unknown concern '{concern}'."),
        };

        Assert.That(
            changed.Fingerprint,
            Is.Not.EqualTo(LanguageProfiles.Version1.Fingerprint)
        );
    }

    [TestCase(UserDefinedFunctionsFeature.Disabled, RecursionFeature.Enabled)]
    [TestCase(UserDefinedFunctionsFeature.Enabled, RecursionFeature.Disabled)]
    public void DependentFunctionOptionsMayBeConfiguredIndependently(
        UserDefinedFunctionsFeature functions,
        RecursionFeature recursion
    )
    {
        Assert.DoesNotThrow(
            () => TestLanguageProfileFactory.Create(
                userDefinedFunctions: functions,
                recursion: recursion
            )
        );
    }

    [Test]
    public void DependentLoopOptionsMayBeConfiguredIndependently()
    {
        Assert.DoesNotThrow(
            static () => TestLanguageProfileFactory.Create(
                loops: LoopFeatures.None,
                multiLevelLoopControl: MultiLevelLoopControlFeature.Enabled
            )
        );
    }

    [Test]
    public void RejectsTruthiness()
    {
        Assert.That(
            static () => TestLanguageProfileFactory.Create(
                conditionSemantics: ConditionSemantics.Truthiness
            ),
            Throws.ArgumentException.With.Property("ParamName")
                .EqualTo("conditionSemantics")
        );
    }

    [TestCaseSource(nameof(InvalidConfigurations))]
    public void RejectsUnknownOptionValues(Func<LanguageProfile> create)
    {
        Assert.That(create, Throws.InstanceOf<ArgumentException>());
    }

    private static IEnumerable<Func<LanguageProfile>> InvalidConfigurations()
    {
        yield return static () => new LanguageProfile(
            (LanguageVersion)99,
            UserDefinedFunctionsFeature.Enabled,
            RecursionFeature.Enabled,
            LoopFeatures.While | LoopFeatures.For,
            ProviderFunctionCallsFeature.Enabled,
            OpenObjectsFeature.Enabled,
            MutationFeatures.ObjectProperties,
            OptionalAccessFeature.Enabled,
            MultiLevelLoopControlFeature.Enabled,
            TrailingCommasFeature.Enabled,
            ConditionSemantics.StrictBoolean,
            ShadowingPolicy.None
        );
        yield return static () => TestLanguageProfileFactory.Create(
            userDefinedFunctions: (UserDefinedFunctionsFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            recursion: (RecursionFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            loops: (LoopFeatures)4
        );
        yield return static () => TestLanguageProfileFactory.Create(
            providerFunctionCalls: (ProviderFunctionCallsFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            openObjects: (OpenObjectsFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            mutations: (MutationFeatures)8
        );
        yield return static () => TestLanguageProfileFactory.Create(
            optionalAccess: (OptionalAccessFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            multiLevelLoopControl: (MultiLevelLoopControlFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            trailingCommas: (TrailingCommasFeature)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            conditionSemantics: (ConditionSemantics)99
        );
        yield return static () => TestLanguageProfileFactory.Create(
            shadowing: (ShadowingPolicy)4
        );
    }

    private static IEnumerable<Action<LanguageProfileBuilder>>
        InvalidBuilderConfigurations()
    {
        yield return static builder =>
            builder.WithLanguageVersion((LanguageVersion)99);
        yield return static builder =>
            builder.WithUserDefinedFunctions((UserDefinedFunctionsFeature)99);
        yield return static builder =>
            builder.WithRecursion((RecursionFeature)99);
        yield return static builder =>
            builder.WithLoops((LoopFeatures)4);
        yield return static builder =>
            builder.EnableLoops((LoopFeatures)4);
        yield return static builder =>
            builder.WithProviderFunctionCalls((ProviderFunctionCallsFeature)99);
        yield return static builder =>
            builder.WithOpenObjects((OpenObjectsFeature)99);
        yield return static builder =>
            builder.WithMutations((MutationFeatures)8);
        yield return static builder =>
            builder.DisableMutations((MutationFeatures)8);
        yield return static builder =>
            builder.WithOptionalAccess((OptionalAccessFeature)99);
        yield return static builder =>
            builder.WithMultiLevelLoopControl((MultiLevelLoopControlFeature)99);
        yield return static builder =>
            builder.WithTrailingCommas((TrailingCommasFeature)99);
        yield return static builder =>
            builder.WithConditionSemantics(ConditionSemantics.Truthiness);
        yield return static builder =>
            builder.WithShadowing((ShadowingPolicy)4);
        yield return static builder =>
            builder.EnableShadowing((ShadowingPolicy)4);
    }
}
