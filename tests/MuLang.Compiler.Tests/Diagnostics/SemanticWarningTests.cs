using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.TestSupport;

namespace MuLang.Compiler.Tests.Diagnostics;

public sealed class SemanticWarningTests
{
    [Test]
    public void WarnsForExplicitEmptyStatements()
    {
        const string source = ";";
        AnalysisResult result = AnalyzeProgram(source);
        Diagnostic diagnostic = RequireWarning(
            result.Diagnostics,
            DiagnosticCodes.RedundantEmptyStatement
        );

        Assert.That(diagnostic.Span, Is.EqualTo(new TextSpan(0, 1)));
    }

    [Test]
    public void DoesNotWarnForParserRecoveryEmptyStatements()
    {
        const string source =
            "var value = 1; func get(): int { return value; }";
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Program
        );
        BindingResult binding = Binder.Bind(
            syntaxTree,
            CreateEmptyEnvironment()
        );

        Assert.That(
            binding.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(DiagnosticCodes.RedundantEmptyStatement)
        );
    }

    [TestCase("if (condition) ;")]
    [TestCase("if (condition) ; else ;")]
    [TestCase("while (condition) ;")]
    [TestCase("for (; condition; ) ;")]
    public void DoesNotWarnForControlStatementBodyEmptyStatements(string source)
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        AnalysisResult result = AnalyzeProgram(source, environment);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(DiagnosticCodes.RedundantEmptyStatement)
        );
    }

    [TestCase("if (1 < 2) { }", "true")]
    [TestCase("if (1 > 2) { }", "false")]
    [TestCase("if (!false) { }", "true")]
    public void WarnsForConstantBooleanConditions(
        string source,
        string expectedValue
    )
    {
        AnalysisResult result = AnalyzeProgram(source);
        Diagnostic diagnostic = RequireWarning(
            result.Diagnostics,
            DiagnosticCodes.ConstantCondition
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Diagnostics.Count(
                    static diagnostic =>
                        diagnostic.Code == DiagnosticCodes.ConstantCondition
                ),
                Is.EqualTo(1)
            );
            Assert.That(
                diagnostic.Message,
                Does.EndWith($"{expectedValue}.")
            );
        }
    }

    [Test]
    public void WarnsForConstantLogicalExpressionsAndOperands()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        AnalysisResult alwaysTrue = AnalyzeExpression(
            "true || condition",
            environment
        );
        AnalysisResult constantOperand = AnalyzeExpression(
            "condition && true",
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            RequireWarning(
                alwaysTrue.Diagnostics,
                DiagnosticCodes.ConstantCondition
            );
            Diagnostic operandWarning = RequireWarning(
                constantOperand.Diagnostics,
                DiagnosticCodes.ConstantCondition
            );
            Assert.That(operandWarning.Span, Is.EqualTo(new TextSpan(0, 17)));
        }
    }

    [TestCase("condition && false")]
    [TestCase("condition || true")]
    public void DoesNotWarnForRightAbsorbingLogicalOperands(string source)
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.condition", "condition", TypeSymbols.Bool)
            .Build();
        AnalysisResult result = AnalyzeExpression(source, environment);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Not.Contain(DiagnosticCodes.ConstantCondition)
        );
    }

    [Test]
    public void WarnsForConstantTruthiness()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            conditionSemantics: ConditionSemantics.Truthiness
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(profile.LanguageVersion);
        AnalysisResult result = AnalyzeProgram(
            "if ([1]) { }",
            environment,
            profile
        );
        Diagnostic diagnostic = RequireWarning(
            result.Diagnostics,
            DiagnosticCodes.ConstantCondition
        );

        Assert.That(diagnostic.Message, Does.EndWith("truthy."));
    }

    [Test]
    public void WarnsForDeterministicNullCoalescing()
    {
        const string leftSource = "(1 as int?) ?? 2";
        const string fallbackSource = "null ?? 2";
        AnalysisResult left = AnalyzeExpression(leftSource);
        AnalysisResult fallback = AnalyzeExpression(fallbackSource);

        using (Assert.EnterMultipleScope())
        {
            Diagnostic leftDiagnostic = RequireWarning(
                left.Diagnostics,
                DiagnosticCodes.RedundantNullCoalescing
            );
            Diagnostic fallbackDiagnostic = RequireWarning(
                fallback.Diagnostics,
                DiagnosticCodes.NullCoalescingAlwaysUsesFallback
            );
            Assert.That(
                leftDiagnostic.Span,
                Is.EqualTo(new TextSpan(0, leftSource.Length))
            );
            Assert.That(
                fallbackDiagnostic.Span,
                Is.EqualTo(new TextSpan(0, fallbackSource.Length))
            );
        }
    }

    [Test]
    public void WarnsForDeterministicOptionalAccessAndNullComparison()
    {
        EnvironmentSchema environment = CreateItemEnvironment();
        AnalysisResult redundantMember = AnalyzeExpression(
            "item?.value",
            environment
        );
        AnalysisResult redundantElement = AnalyzeExpression(
            "item?.[\"value\"]",
            environment
        );
        AnalysisResult alwaysNull = AnalyzeExpression(
            "(null as Item?)?.value",
            environment
        );
        const string nullComparisonSource = "item == null";
        AnalysisResult nullComparison = AnalyzeExpression(
            nullComparisonSource,
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            RequireWarning(
                redundantMember.Diagnostics,
                DiagnosticCodes.RedundantOptionalAccess
            );
            RequireWarning(
                redundantElement.Diagnostics,
                DiagnosticCodes.RedundantOptionalAccess
            );
            RequireWarning(
                alwaysNull.Diagnostics,
                DiagnosticCodes.OptionalAccessAlwaysNull
            );
            Diagnostic comparisonDiagnostic = RequireWarning(
                nullComparison.Diagnostics,
                DiagnosticCodes.ConstantNullComparison
            );
            Assert.That(
                comparisonDiagnostic.Span,
                Is.EqualTo(new TextSpan(0, nullComparisonSource.Length))
            );
        }
    }

    [Test]
    public void DoesNotWarnWhenNullnessIsUnknown()
    {
        EnvironmentSchema environment = CreateItemEnvironment();
        AnalysisResult optionalAccess = AnalyzeExpression(
            "optionalItem?.value",
            environment
        );
        AnalysisResult comparison = AnalyzeExpression(
            "optionalItem == null",
            environment
        );
        AnalysisResult coalescing = AnalyzeExpression(
            "optionalItem ?? item",
            environment
        );
        AnalysisResult dynamicAccess = AnalyzeExpression(
            "dynamicItem?.missing",
            environment
        );
        AnalysisResult optionalPropertyAccess = AnalyzeExpression(
            "item?.optionalValue",
            environment
        );
        object[] nullnessWarnings =
        [
            DiagnosticCodes.RedundantOptionalAccess,
            DiagnosticCodes.OptionalAccessAlwaysNull,
            DiagnosticCodes.ConstantNullComparison,
            DiagnosticCodes.RedundantNullCoalescing,
            DiagnosticCodes.NullCoalescingAlwaysUsesFallback,
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                optionalAccess.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Has.None.AnyOf(nullnessWarnings)
            );
            Assert.That(
                comparison.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Has.None.AnyOf(nullnessWarnings)
            );
            Assert.That(
                coalescing.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Has.None.AnyOf(nullnessWarnings)
            );
            Assert.That(
                dynamicAccess.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Does.Not.Contain(DiagnosticCodes.RedundantOptionalAccess)
            );
            Assert.That(
                optionalPropertyAccess.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Does.Not.Contain(DiagnosticCodes.RedundantOptionalAccess)
            );
        }
    }

    [Test]
    public void WarnsForAlwaysTrueTypeTestsAndEquivalentCasts()
    {
        EnvironmentSchema environment = CreateItemEnvironment();
        AnalysisResult typeTest = AnalyzeExpression(
            "item is object",
            environment
        );
        const string redundantCastSource = "item as Item";
        AnalysisResult redundantCast = AnalyzeExpression(
            redundantCastSource,
            environment
        );
        AnalysisResult wideningCast = AnalyzeExpression(
            "1 as number",
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            RequireWarning(
                typeTest.Diagnostics,
                DiagnosticCodes.AlwaysTrueTypeTest
            );
            Diagnostic castDiagnostic = RequireWarning(
                redundantCast.Diagnostics,
                DiagnosticCodes.RedundantCast
            );
            Assert.That(
                castDiagnostic.Span,
                Is.EqualTo(new TextSpan(0, redundantCastSource.Length))
            );
            Assert.That(
                wideningCast.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Does.Not.Contain(DiagnosticCodes.RedundantCast)
            );
        }
    }

    [Test]
    public void WarnsForAdditionalAlwaysTrueTypeTests()
    {
        ObjectTypeSymbol sourceType = new (
            "type.source",
            "Source",
            false,
            [
                new ObjectPropertySymbol("value", TypeSymbols.Int),
                new ObjectPropertySymbol("label", TypeSymbols.String),
            ]
        );
        ObjectTypeSymbol targetType = new (
            "type.target",
            "Target",
            true,
            [ new ObjectPropertySymbol("value", TypeSymbols.Number) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(sourceType)
            .AddType(targetType)
            .AddGlobal("global.values", "values", TypeSymbols.Array(TypeSymbols.Int))
            .AddGlobal("global.item", "item", sourceType)
            .Build();
        AnalysisResult array = AnalyzeExpression(
            "values is number[]",
            environment
        );
        AnalysisResult structuredObject = AnalyzeExpression(
            "item is Target",
            environment
        );
        AnalysisResult constant = AnalyzeExpression(
            "(1 as number) is int",
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            RequireWarning(
                array.Diagnostics,
                DiagnosticCodes.AlwaysTrueTypeTest
            );
            RequireWarning(
                structuredObject.Diagnostics,
                DiagnosticCodes.AlwaysTrueTypeTest
            );
            RequireWarning(
                constant.Diagnostics,
                DiagnosticCodes.AlwaysTrueTypeTest
            );
        }
    }

    [Test]
    public void PreservesCastsThatContributeToTypeInference()
    {
        AnalysisResult local = AnalyzeProgram("var value = 4 as number;");
        AnalysisResult array = AnalyzeProgram(
            "var values = [1, 2.5 as number];"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                local.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.RedundantCast)
            );
            Assert.That(
                array.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.RedundantCast)
            );
        }
    }

    [TestCase("var value: number = 4 as number;")]
    [TestCase("var values: number[] = [1, 2.5 as number];")]
    [TestCase("var value: object = {} as object;")]
    public void WarnsForGuaranteedCastsInTypedContexts(string source)
    {
        AnalysisResult result = AnalyzeProgram(source);

        RequireWarning(
            result.Diagnostics,
            DiagnosticCodes.RedundantCast
        );
    }

    [Test]
    public void ReportsWarningsWhenConstantFoldingIsDisabled()
    {
        LanguageProfile profile = new LanguageProfileBuilder()
            .WithConstantFolding(ConstantFoldingFeature.Disabled)
            .Build();
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(profile.LanguageVersion);
        AnalysisResult result = AnalyzeProgram(
            "if (1 < 2) { }",
            environment,
            profile
        );

        RequireWarning(
            result.Diagnostics,
            DiagnosticCodes.ConstantCondition
        );
    }

    [Test]
    public void WarnsForFailingConstantsWhenConstantFoldingIsDisabled()
    {
        LanguageProfile profile = new LanguageProfileBuilder()
            .WithConstantFolding(ConstantFoldingFeature.Disabled)
            .Build();
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(profile.LanguageVersion);
        AnalysisResult result = AnalyzeProgram(
            "if (1 / 0 == 0) { }",
            environment,
            profile
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics.HasErrors, Is.False);
            Diagnostic diagnostic = RequireWarning(
                result.Diagnostics,
                DiagnosticCodes.ConstantEvaluationFailed
            );
            Assert.That(diagnostic.Span, Is.EqualTo(new TextSpan(4, 5)));
        }
    }

    private static AnalysisResult AnalyzeExpression(
        string source,
        EnvironmentSchema? environment = null,
        LanguageProfile? profile = null
    )
    {
        environment ??= CreateEmptyEnvironment(
            profile?.LanguageVersion ?? LanguageVersion.Version1_2
        );

        return MuLangCompiler.Analyze(
            source,
            environment,
            CompilationMode.Expression,
            profile: profile
        );
    }

    private static AnalysisResult AnalyzeProgram(
        string source,
        EnvironmentSchema? environment = null,
        LanguageProfile? profile = null
    )
    {
        environment ??= CreateEmptyEnvironment(
            profile?.LanguageVersion ?? LanguageVersion.Version1_2
        );

        return MuLangCompiler.Analyze(
            source,
            environment,
            CompilationMode.Program,
            TypeSymbols.Void,
            profile
        );
    }

    private static Diagnostic RequireWarning(
        DiagnosticCollection diagnostics,
        string code
    )
    {
        Diagnostic diagnostic = diagnostics.Single(
            diagnostic => diagnostic.Code == code
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostics.HasErrors, Is.False);
        }

        return diagnostic;
    }

    private static EnvironmentSchema CreateEmptyEnvironment(
        LanguageVersion languageVersion = LanguageVersion.Version1_1
    )
    {
        return new EnvironmentBuilder().Build(languageVersion);
    }

    private static EnvironmentSchema CreateItemEnvironment()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [
                new ObjectPropertySymbol("value", TypeSymbols.Int),
                new ObjectPropertySymbol(
                    "optionalValue",
                    TypeSymbols.Int,
                    true
                ),
            ]
        );

        return new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .AddGlobal(
                "global.optionalItem",
                "optionalItem",
                TypeSymbols.Nullable(itemType)
            )
            .AddGlobal("global.dynamicItem", "dynamicItem", TypeSymbols.Object)
            .Build();
    }
}
