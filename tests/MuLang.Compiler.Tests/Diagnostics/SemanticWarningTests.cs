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
            "condition || true",
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
            Assert.That(operandWarning.Span, Is.EqualTo(new TextSpan(13, 4)));
        }
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
        AnalysisResult left = AnalyzeExpression("(1 as int?) ?? 2");
        AnalysisResult fallback = AnalyzeExpression("null ?? 2");

        using (Assert.EnterMultipleScope())
        {
            RequireWarning(
                left.Diagnostics,
                DiagnosticCodes.RedundantNullCoalescing
            );
            RequireWarning(
                fallback.Diagnostics,
                DiagnosticCodes.NullCoalescingAlwaysUsesFallback
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
        AnalysisResult nullComparison = AnalyzeExpression(
            "item == null",
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
            RequireWarning(
                nullComparison.Diagnostics,
                DiagnosticCodes.ConstantNullComparison
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
        AnalysisResult redundantCast = AnalyzeExpression(
            "item as Item",
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
            RequireWarning(
                redundantCast.Diagnostics,
                DiagnosticCodes.RedundantCast
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
    public void DoesNotEvaluateFailingConstantsForWarnings()
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
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.ConstantCondition)
            );
        }
    }

    private static AnalysisResult AnalyzeExpression(
        string source,
        EnvironmentSchema? environment = null,
        LanguageProfile? profile = null
    )
    {
        environment ??= CreateEmptyEnvironment(
            profile?.LanguageVersion ?? LanguageVersion.Version1_1
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
            profile?.LanguageVersion ?? LanguageVersion.Version1_1
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
