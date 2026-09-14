using MuLang.Compiler.Diagnostics;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Tests.Language;

public sealed class FeatureFlagTests
{
    [Test]
    public void DisablesUserFunctionDeclarationsAndCallsWithRecovery()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            userDefinedFunctions: UserDefinedFunctionsFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            """
            func value(): int { return 1; }
            value();
            missing();
            """,
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnosticCount(
                result,
                DiagnosticCodes.DisabledUserDefinedFunctions,
                2
            );
            AssertDiagnostic(result, DiagnosticCodes.UndefinedFunction);
        }
    }

    [Test]
    public void RecursionOptionIsDormantWhenUserFunctionsAreDisabled()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            userDefinedFunctions: UserDefinedFunctionsFeature.Disabled,
            recursion: RecursionFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            "func recurse(): void { recurse(); }",
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(result, DiagnosticCodes.DisabledUserDefinedFunctions);
            AssertNoDiagnostic(result, DiagnosticCodes.DisabledRecursion);
        }
    }

    [TestCase(
        "func first(): void { first(); }",
        1
    )]
    [TestCase(
        """
        func first(): void { second(); }
        func second(): void { first(); }
        """,
        2
    )]
    [TestCase(
        """
        func first(): void { second(); }
        func second(): void { third(); }
        func third(): void { first(); }
        """,
        3
    )]
    public void RejectsEveryFunctionInARecursiveComponent(
        string source,
        int expectedCount
    )
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            recursion: RecursionFeature.Disabled
        );
        CompilationResult result = CompileProgram(source, profile);

        AssertDiagnosticCount(
            result,
            DiagnosticCodes.DisabledRecursion,
            expectedCount
        );
    }

    [Test]
    public void RejectsMultipleIndependentRecursiveComponentsWithoutDuplication()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            recursion: RecursionFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            """
            func first(): void { first(); }
            func second(): void { third(); }
            func third(): void { second(); }
            func fourth(): void { fourth(); }
            """,
            profile
        );

        AssertDiagnosticCount(result, DiagnosticCodes.DisabledRecursion, 4);
    }

    [Test]
    public void AllowsAcyclicUserFunctionCallsWhenRecursionIsDisabled()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            recursion: RecursionFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            """
            func first(): void { second(); }
            func second(): void { return; }
            first();
            """,
            profile
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ExcludesProviderCallsFromRecursionAnalysis()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("provider.next", "next", [ ], TypeSymbols.Void)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            recursion: RecursionFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            "func first(): void { next(); } first();",
            profile,
            environment
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase(LoopFeatures.For, DiagnosticCodes.DisabledWhileLoop)]
    [TestCase(LoopFeatures.While, DiagnosticCodes.DisabledForLoop)]
    [TestCase(LoopFeatures.None, DiagnosticCodes.DisabledWhileLoop)]
    [TestCase(LoopFeatures.None, DiagnosticCodes.DisabledForLoop)]
    public void ControlsLoopKindsIndependently(
        LoopFeatures loops,
        string expectedDiagnostic
    )
    {
        string source = expectedDiagnostic == DiagnosticCodes.DisabledWhileLoop
            ? "while (false) { }"
            : "for (; false; ) { }";
        LanguageProfile profile = TestLanguageProfileFactory.Create(loops: loops);
        CompilationResult result = CompileProgram(source, profile);

        AssertDiagnostic(result, expectedDiagnostic);
    }

    [Test]
    public void BindsDisabledLoopBodiesForIndependentDiagnostics()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            loops: LoopFeatures.For
        );
        CompilationResult result = CompileProgram(
            "while (false) { missing(); }",
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(result, DiagnosticCodes.DisabledWhileLoop);
            AssertDiagnostic(result, DiagnosticCodes.UndefinedFunction);
        }
    }

    [Test]
    public void DisablesExplicitLoopControlLevelsIncludingOne()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            multiLevelLoopControl: MultiLevelLoopControlFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            "while (true) { break 1; }",
            profile
        );

        AssertDiagnosticCount(
            result,
            DiagnosticCodes.DisabledMultiLevelLoopControl,
            1
        );
    }

    [Test]
    public void LoopControlOptionIsDormantWhenAllLoopsAreDisabled()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            loops: LoopFeatures.None,
            multiLevelLoopControl: MultiLevelLoopControlFeature.Disabled
        );
        CompilationResult result = CompileProgram(
            "while (true) { break 1; }",
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(result, DiagnosticCodes.DisabledWhileLoop);
            AssertNoDiagnostic(
                result,
                DiagnosticCodes.DisabledMultiLevelLoopControl
            );
        }
    }

    [Test]
    public void DisablesResolvedProviderFunctionCallsWithoutRejectingTheEnvironment()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("provider.log", "log", [ ], TypeSymbols.Void)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            providerFunctionCalls: ProviderFunctionCallsFeature.Disabled
        );
        CompilationResult used = CompileProgram("log();", profile, environment);
        CompilationResult unused = CompileProgram("", profile, environment);

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(
                used,
                DiagnosticCodes.DisabledProviderFunctionCalls
            );
            Assert.That(unused.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void RejectsOpenObjectLiteralsWithoutDuplicateDiagnostics()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            "@{ }",
            CreateEmptyEnvironment(),
            TypeSymbols.Object,
            profile
        );

        AssertDiagnosticCount(result, DiagnosticCodes.DisabledOpenObjects, 1);
    }

    [Test]
    public void RejectsAnyOpenStructuredTypeInTheEnvironmentEvenWhenUnused()
    {
        ObjectTypeSymbol openType = new ("type.open", "Open", true, [ ]);
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(openType)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = CompileProgram("", profile, environment);

        AssertDiagnosticCount(result, DiagnosticCodes.DisabledOpenObjects, 1);
    }

    [TestCase("value.member")]
    [TestCase("value[\"member\"]")]
    [TestCase("value has \"member\"")]
    public void GenericObjectExposesNoDynamicOperationsWhenOpenObjectsAreDisabled(
        string source
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Object)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            source,
            environment,
            null,
            profile
        );

        AssertDiagnostic(result, DiagnosticCodes.DisabledOpenObjects);
    }

    [Test]
    public void ClosedObjectKnownPropertiesRemainAvailableWithoutOpenObjects()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            "item.value",
            environment,
            TypeSymbols.Int,
            profile
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void RejectsDynamicPropertyTestsOnClosedObjectsWithoutOpenObjects()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .AddGlobal("global.key", "key", TypeSymbols.String)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            "item has key",
            environment,
            TypeSymbols.Bool,
            profile
        );

        AssertDiagnostic(result, DiagnosticCodes.DisabledOpenObjects);
    }

    [Test]
    public void ReportsIndependentOpenObjectAndMutationRestrictions()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Object)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled,
            mutations: MutationFeatures.None
        );
        CompilationResult result = MuLangCompiler.CompileProgram(
            "value.member = 1;",
            environment,
            TypeSymbols.Void,
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(result, DiagnosticCodes.DisabledOpenObjects);
            AssertDiagnostic(
                result,
                DiagnosticCodes.DisabledObjectPropertyMutation
            );
        }
    }

    [Test]
    public void ClosedObjectPropertyTestsRemainAvailableWithoutOpenObjects()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            openObjects: OpenObjectsFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            "item has \"value\"",
            environment,
            TypeSymbols.Bool,
            profile
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase("item.value = 1;")]
    [TestCase("item[\"value\"] = 1;")]
    public void DisablesObjectPropertyAssignment(string source)
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            mutations: MutationFeatures.ArrayElements |
            MutationFeatures.PropertyRemoval
        );
        CompilationResult result = CompileProgram(source, profile, environment);

        AssertDiagnosticCount(
            result,
            DiagnosticCodes.DisabledObjectPropertyMutation,
            1
        );
    }

    [Test]
    public void DisablesArrayElementAssignmentIndependently()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            mutations: MutationFeatures.ObjectProperties |
            MutationFeatures.PropertyRemoval
        );
        CompilationResult result = CompileProgram(
            "var values: int[] = [1]; values[0] = 2;",
            profile
        );

        AssertDiagnostic(
            result,
            DiagnosticCodes.DisabledArrayElementMutation
        );
    }

    [Test]
    public void DisablesPropertyRemovalIndependentlyWithoutDuplicateDiagnostics()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int, true) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            mutations: MutationFeatures.ObjectProperties |
            MutationFeatures.ArrayElements
        );
        CompilationResult result = CompileProgram(
            "item.value~;",
            profile,
            environment
        );

        AssertDiagnosticCount(
            result,
            DiagnosticCodes.DisabledPropertyRemoval,
            1
        );
    }

    [TestCase("item?.value")]
    [TestCase("item?.[\"value\"]")]
    public void DisablesOptionalAccessWithRecovery(string source)
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal(
                "global.item",
                "item",
                TypeSymbols.Nullable(itemType)
            )
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            optionalAccess: OptionalAccessFeature.Disabled
        );
        CompilationResult result = MuLangCompiler.CompileExpression(
            source,
            environment,
            null,
            profile
        );

        AssertDiagnosticCount(result, DiagnosticCodes.DisabledOptionalAccess, 1);
    }

    [TestCase("var values: int[] = [1,];")]
    [TestCase("var value = { item: 1, };")]
    [TestCase("var value = @{ item: 1, };")]
    public void DisablesTrailingCommasInEveryLiteralKind(string source)
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            trailingCommas: TrailingCommasFeature.Disabled
        );
        CompilationResult result = CompileProgram(source, profile);

        AssertDiagnosticCount(result, DiagnosticCodes.DisabledTrailingCommas, 1);
    }

    [Test]
    public void CallTrailingCommasRemainOrdinarySyntaxErrors()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction(
                "provider.use",
                "use",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.Void
            )
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            trailingCommas: TrailingCommasFeature.Enabled
        );
        CompilationResult result = CompileProgram("use(1,);", profile, environment);

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(result, DiagnosticCodes.TrailingSeparator);
            AssertNoDiagnostic(result, DiagnosticCodes.DisabledTrailingCommas);
        }
    }

    [TestCase("var values: int[] = [];")]
    [TestCase("var value = { };")]
    public void EmptyLiteralsRemainValidWhenTrailingCommasAreDisabled(
        string source
    )
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            trailingCommas: TrailingCommasFeature.Disabled
        );
        CompilationResult result = CompileProgram(source, profile);

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase(ShadowingPolicy.None, false, false)]
    [TestCase(ShadowingPolicy.NestedScopes, true, false)]
    [TestCase(ShadowingPolicy.Globals, false, true)]
    [TestCase(
        ShadowingPolicy.NestedScopes | ShadowingPolicy.Globals,
        true,
        true
    )]
    public void AppliesShadowingPermissionsIndependently(
        ShadowingPolicy shadowing,
        bool nestedAllowed,
        bool globalAllowed
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            shadowing: shadowing
        );
        CompilationResult nested = CompileProgram(
            "var local = 1; { var local = 2; }",
            profile,
            environment
        );
        CompilationResult global = CompileProgram(
            "var value = 1;",
            profile,
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                HasDiagnostic(nested, DiagnosticCodes.ShadowedVariable),
                Is.EqualTo(!nestedAllowed)
            );
            Assert.That(
                HasDiagnostic(global, DiagnosticCodes.ShadowedVariable),
                Is.EqualTo(!globalAllowed)
            );
        }
    }

    [Test]
    public void BothShadowingPermissionsAreRequiredWhenBothKindsApply()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            shadowing: ShadowingPolicy.NestedScopes
        );
        CompilationResult result = CompileProgram(
            "var value = 1; { var value = 2; }",
            profile,
            environment
        );

        AssertDiagnostic(result, DiagnosticCodes.ShadowedVariable);
    }

    [Test]
    public void FunctionParametersMayShadowGlobalsOnlyWhenAllowed()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        LanguageProfile denied = TestLanguageProfileFactory.Create();
        LanguageProfile allowed = TestLanguageProfileFactory.Create(
            shadowing: ShadowingPolicy.Globals
        );
        const string source = "func use(value: int): void { return; }";

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(
                CompileProgram(source, denied, environment),
                DiagnosticCodes.ShadowedVariable
            );
            Assert.That(
                CompileProgram(source, allowed, environment).Diagnostics,
                Is.Empty
            );
        }
    }

    [Test]
    public void NestedScopesMayShadowFunctionParametersOnlyWhenAllowed()
    {
        const string source = """
                              func use(value: int): void {
                                  {
                                      var value = 1;
                                  }
                              }
                              """;
        LanguageProfile denied = TestLanguageProfileFactory.Create();
        LanguageProfile allowed = TestLanguageProfileFactory.Create(
            shadowing: ShadowingPolicy.NestedScopes
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(
                CompileProgram(source, denied),
                DiagnosticCodes.ShadowedVariable
            );
            Assert.That(CompileProgram(source, allowed).Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void SameScopeDuplicatesRemainInvalidWithAllShadowingPermissions()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            shadowing: ShadowingPolicy.NestedScopes | ShadowingPolicy.Globals
        );
        CompilationResult result = CompileProgram(
            "var value = 1; var value = 2;",
            profile
        );

        AssertDiagnostic(result, DiagnosticCodes.DuplicateLocal);
    }

    [Test]
    public void ExplicitStandardProfileMatchesLegacyOverload()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        CompilationResult legacy = MuLangCompiler.CompileExpression(
            "40 + 2",
            environment,
            TypeSymbols.Int
        );
        CompilationResult explicitProfile = MuLangCompiler.CompileExpression(
            "40 + 2",
            environment,
            TypeSymbols.Int,
            LanguageProfiles.Version1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(legacy.Diagnostics, Is.Empty);
            Assert.That(explicitProfile.Diagnostics, Is.Empty);
            Assert.That(explicitProfile.IsSuccessful, Is.EqualTo(legacy.IsSuccessful));
        }
    }

    [Test]
    public void StandardProfileUsesStrictBooleanConditions()
    {
        CompilationResult result = CompileProgram(
            "if (1) { }",
            LanguageProfiles.Version1
        );

        AssertDiagnostic(result, DiagnosticCodes.TypeMismatch);
    }

    private static CompilationResult CompileProgram(
        string source,
        LanguageProfile profile,
        EnvironmentSchema? environment = null
    )
    {
        return MuLangCompiler.CompileProgram(
            source,
            environment ?? CreateEmptyEnvironment(),
            TypeSymbols.Void,
            profile
        );
    }

    private static EnvironmentSchema CreateEmptyEnvironment()
    {
        return new EnvironmentBuilder().Build();
    }

    private static bool HasDiagnostic(CompilationResult result, string code)
    {
        return result.Diagnostics.Any(diagnostic => diagnostic.Code == code);
    }

    private static void AssertDiagnostic(CompilationResult result, string code)
    {
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(code)
        );
    }

    private static void AssertNoDiagnostic(CompilationResult result, string code)
    {
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(code)
        );
    }

    private static void AssertDiagnosticCount(
        CompilationResult result,
        string code,
        int expectedCount
    )
    {
        Assert.That(
            result.Diagnostics.Count(diagnostic => diagnostic.Code == code),
            Is.EqualTo(expectedCount)
        );
    }
}
