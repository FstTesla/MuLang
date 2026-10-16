using MuLang.Compiler.Diagnostics;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests;

public sealed class MuLangCompilerEditorTests
{
    [Test]
    public void LanguageSamplesHaveExpectedDiagnostics()
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                ".."
            )
        );
        string samplesRoot = Path.Combine(repositoryRoot, "docs", "samples");
        IReadOnlyList<string> validFiles =
        [
            .. Directory.GetFiles(
                Path.Combine(samplesRoot, "valid"),
                "*.mu",
                SearchOption.AllDirectories
            ),
        ];
        IReadOnlyList<string> warningFiles =
        [
            .. Directory.GetFiles(
                Path.Combine(samplesRoot, "warnings"),
                "*.mu",
                SearchOption.AllDirectories
            ),
        ];
        IReadOnlyList<string> errorFiles =
        [
            .. Directory.GetFiles(
                Path.Combine(samplesRoot, "errors"),
                "*.mu",
                SearchOption.AllDirectories
            ),
        ];
        EnvironmentSchema environment = new EnvironmentBuilder()
            .Build(LanguageVersion.Version1_2);
        IReadOnlyList<Diagnostic> validDiagnostics =
        [
            .. validFiles.SelectMany(
                file => AnalyzeSample(file, environment).Diagnostics
            ),
        ];
        IReadOnlyList<Diagnostic> warningDiagnostics =
        [
            .. warningFiles.SelectMany(
                file => AnalyzeSample(file, environment).Diagnostics
            ),
        ];
        IReadOnlyList<IReadOnlyList<Diagnostic>> errorDiagnostics =
        [
            .. errorFiles.Select(
                file => AnalyzeSample(file, environment).Diagnostics
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(validDiagnostics, Is.Empty);
            Assert.That(warningDiagnostics, Is.Not.Empty);
            Assert.That(
                warningDiagnostics,
                Has.All.Matches<Diagnostic>(
                    static diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Warning
                )
            );
            Assert.That(errorDiagnostics, Is.Not.Empty);
            Assert.That(
                errorDiagnostics,
                Has.All.Matches<IReadOnlyList<Diagnostic>>(
                    static diagnostics =>
                        diagnostics.Any(
                            static diagnostic =>
                                diagnostic.Severity == DiagnosticSeverity.Error
                        ) &&
                        diagnostics.All(
                            static diagnostic =>
                                diagnostic.Category is not
                                    DiagnosticCategory.Lexical and not
                                    DiagnosticCategory.Syntax
                        )
                )
            );
        }

        static AnalysisResult AnalyzeSample(
            string path,
            EnvironmentSchema environment
        ) =>
            MuLangCompiler.Analyze(
                File.ReadAllText(path),
                environment,
                CompilationMode.Program,
                new MuLangCompilationOptions
                {
                    Profile = LanguageProfiles.Version1_2,
                }
            );
    }

    [Test]
    public void TypeSampleClassifiesUserDefinedTypes()
    {
        string repositoryRoot = Path.GetFullPath(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                ".."
            )
        );
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "docs",
                "samples",
                "valid",
                "types-and-objects.mu"
            )
        );
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            source,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            profile: LanguageProfiles.Version1_2
        );
        SourceText sourceText = SourceText.From(source);

        Assert.That(
            result.Classifications.Any(
                classification =>
                    classification.Kind == SemanticClassificationKind.Type &&
                    classification.Modifiers.HasFlag(
                        SemanticClassificationModifiers.Declaration
                    ) &&
                    sourceText.GetText(classification.Span) == "Node"
            ),
            Is.True
        );
    }

    [Test]
    public void AnalyzesWithoutProducingPortableIr()
    {
        AnalysisResult result = MuLangCompiler.Analyze(
            "var value: int = \"text\";",
            new EnvironmentBuilder().Build(),
            CompilationMode.Program,
            MuLangCompilationOptions.Default
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.TypeMismatch));
        }
    }

    [Test]
    public void ClassifiesEditorOrientedTokenKinds()
    {
        ClassificationResult result = MuLangCompiler.Classify(
            "var value = infty + \"text\";"
        );

        Assert.That(
            result.Classifications.Select(static classification => classification.Kind),
            Is.EqualTo(
                [
                    SourceClassificationKind.Keyword,
                    SourceClassificationKind.Identifier,
                    SourceClassificationKind.Operator,
                    SourceClassificationKind.Number,
                    SourceClassificationKind.Operator,
                    SourceClassificationKind.String,
                    SourceClassificationKind.Punctuation,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ClassifiesExceptionHandlingKeywords()
    {
        ClassificationResult result = MuLangCompiler.Classify(
            "catch finally throw try",
            LanguageProfiles.Version1_2
        );

        Assert.That(
            result.Classifications.Select(static classification => classification.Kind),
            Is.EqualTo(
                [
                    SourceClassificationKind.Keyword,
                    SourceClassificationKind.Keyword,
                    SourceClassificationKind.Keyword,
                    SourceClassificationKind.Keyword,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ClassifiesSourceAndInlineObjectTypes()
    {
        const string sourceStr = """
                                 type Node { next?: Node, payload: { value: int } };
                                 var node: Node;
                                 """;
        SourceText source = SourceText.From(sourceStr);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            sourceStr,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            profile: LanguageProfiles.Version1_2
        );
        IReadOnlyList<(
            string Text,
            SemanticClassificationKind Kind,
            SemanticClassificationModifiers Modifiers
            )> classifications =
        [
            .. result.Classifications.Select(
                classification => (
                    source.GetText(classification.Span),
                    classification.Kind,
                    classification.Modifiers
                )
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "Node",
                        SemanticClassificationKind.Type,
                        SemanticClassificationModifiers.Declaration
                    )
                )
            );
            Assert.That(
                classifications.Count(
                    static classification =>
                        classification is
                        {
                            Text: "Node",
                            Kind: SemanticClassificationKind.Type,
                        }
                ),
                Is.EqualTo(3)
            );
            Assert.That(
                classifications.Count(
                    static classification =>
                        classification.Kind ==
                        SemanticClassificationKind.Property
                ),
                Is.EqualTo(3)
            );
        }
    }

    [Test]
    public void ClassifiesSymbolsInExceptionRegions()
    {
        const string text = """
                            var value = 1;
                            try { var copy = value; }
                            catch (failure) { var message = failure.message; throw failure; }
                            finally { value = 2; }
                            """;
        SourceText source = SourceText.From(text);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            text,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Diagnostics.HasErrors,
                Is.False,
                string.Join("\n", result.Diagnostics.Select(static diagnostic => diagnostic.Message))
            );
            Assert.That(
                result.Classifications.Select(
                    classification => source.GetText(classification.Span)
                ),
                Is.EqualTo(["value", "copy", "value", "failure", "message", "failure", "message", "failure", "value"])
            );
            Assert.That(
                result.Classifications[3].Modifiers,
                Is.EqualTo(
                    SemanticClassificationModifiers.Declaration |
                    SemanticClassificationModifiers.ReadOnly
                )
            );
        }
    }

    [Test]
    public void PreservesScalarClassificationSpans()
    {
        ClassificationResult result = MuLangCompiler.Classify("😀");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Classifications, Has.Count.EqualTo(1));
            Assert.That(result.Classifications[0].Kind, Is.EqualTo(SourceClassificationKind.Invalid));
            Assert.That(result.Classifications[0].Span, Is.EqualTo(new TextSpan(0, 1)));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }
    }

    [Test]
    public void ClassifiesBoundDeclarationsAndReferences()
    {
        ObjectTypeSymbol customerType = new (
            "type.customer",
            "Customer",
            false,
            [ new ObjectPropertySymbol("name", TypeSymbols.String, false) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(customerType)
            .AddGlobal("global.customer", "customer", customerType)
            .AddFunction(
                "function.load",
                "load",
                [ new ParameterSymbol("value", customerType) ],
                customerType
            )
            .Build();
        const string sourceStr = """
                                 func calculate(input: Customer): Customer {
                                     var current: Customer = input;
                                     return current;
                                 }
                                 var result: Customer = calculate(load(customer));
                                 var property = customer.name;
                                 var literal = { label = 1 };
                                 """;
        SourceText source = SourceText.From(sourceStr);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            sourceStr,
            environment,
            CompilationMode.Program
        );
        IReadOnlyList<(
            string Text,
            SemanticClassificationKind Kind,
            SemanticClassificationModifiers Modifiers
            )> classifications =
        [
            .. result.Classifications.Select(
                classification => (
                    source.GetText(classification.Span),
                    classification.Kind,
                    classification.Modifiers
                )
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "calculate",
                        SemanticClassificationKind.Function,
                        SemanticClassificationModifiers.Declaration
                    )
                )
            );
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "load",
                        SemanticClassificationKind.Function,
                        SemanticClassificationModifiers.DefaultLibrary
                    )
                )
            );
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "input",
                        SemanticClassificationKind.Parameter,
                        SemanticClassificationModifiers.Declaration
                    )
                )
            );
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "customer",
                        SemanticClassificationKind.Variable,
                        SemanticClassificationModifiers.ReadOnly |
                        SemanticClassificationModifiers.DefaultLibrary
                    )
                )
            );
            Assert.That(
                classifications.Count(
                    static classification =>
                        classification is { Text: "Customer", Kind: SemanticClassificationKind.Type }
                ),
                Is.EqualTo(4)
            );
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "name",
                        SemanticClassificationKind.Property,
                        SemanticClassificationModifiers.None
                    )
                )
            );
            Assert.That(
                classifications,
                Does.Contain(
                    (
                        "label",
                        SemanticClassificationKind.Property,
                        SemanticClassificationModifiers.Declaration
                    )
                )
            );
        }
    }

    [Test]
    public void ClassifiesReferencesRemovedByConstantFolding()
    {
        const string sourceStr = """
                                 var first = 1;
                                 var second = true ? first : first;
                                 """;
        SourceText source = SourceText.From(sourceStr);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            sourceStr,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program
        );

        Assert.That(
            result.Classifications.Count(
                classification =>
                    classification.Kind == SemanticClassificationKind.Variable &&
                    source.GetText(classification.Span) == "first"
            ),
            Is.EqualTo(3)
        );
    }

    [Test]
    public void ClassifiesReadOnlyLocalDeclarationsAndReferences()
    {
        const string sourceStr = "var value$ = 1; var copy = value;";
        SourceText source = SourceText.From(sourceStr);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            sourceStr,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program
        );
        IReadOnlyList<SemanticClassificationModifiers> modifiers =
        [
            .. result.Classifications
                .Where(
                    classification =>
                        classification.Kind == SemanticClassificationKind.Variable &&
                        source.GetText(classification.Span) == "value"
                )
                .Select(static classification => classification.Modifiers),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                modifiers,
                Is.EqualTo(
                    [
                        SemanticClassificationModifiers.Declaration |
                        SemanticClassificationModifiers.ReadOnly,
                        SemanticClassificationModifiers.ReadOnly,
                    ]
                )
            );
        }
    }

    [Test]
    public void ClassifiesImplicitConversions()
    {
        const string sourceStr = "var value: number = 1;";
        SourceText source = SourceText.From(sourceStr);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            sourceStr,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program
        );
        SemanticClassification classification = result.Classifications.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                classification.Kind,
                Is.EqualTo(SemanticClassificationKind.Variable)
            );
            Assert.That(
                classification.Modifiers,
                Is.EqualTo(SemanticClassificationModifiers.Declaration)
            );
            Assert.That(
                source.GetText(classification.Span),
                Is.EqualTo("value")
            );
        }
    }
}
