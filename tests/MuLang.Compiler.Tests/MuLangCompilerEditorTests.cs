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
    public void AnalyzesWithoutProducingPortableIr()
    {
        AnalysisResult result = MuLangCompiler.Analyze(
            "var value: int = \"text\";",
            new EnvironmentBuilder().Build(),
            CompilationMode.Program
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
        const string Source = """
            func calculate(input: Customer): Customer {
                var current: Customer = input;
                return current;
            }
            var result: Customer = calculate(load(customer));
            var property = customer.name;
            var literal = { label: 1 };
            """;
        SourceText source = SourceText.From(Source);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            Source,
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
                Does.Contain((
                    "calculate",
                    SemanticClassificationKind.Function,
                    SemanticClassificationModifiers.Declaration
                ))
            );
            Assert.That(
                classifications,
                Does.Contain((
                    "load",
                    SemanticClassificationKind.Function,
                    SemanticClassificationModifiers.DefaultLibrary
                ))
            );
            Assert.That(
                classifications,
                Does.Contain((
                    "input",
                    SemanticClassificationKind.Parameter,
                    SemanticClassificationModifiers.Declaration
                ))
            );
            Assert.That(
                classifications,
                Does.Contain((
                    "customer",
                    SemanticClassificationKind.Variable,
                    SemanticClassificationModifiers.ReadOnly |
                    SemanticClassificationModifiers.DefaultLibrary
                ))
            );
            Assert.That(
                classifications.Count(
                    static classification =>
                        classification.Text == "Customer" &&
                        classification.Kind == SemanticClassificationKind.Type
                ),
                Is.EqualTo(4)
            );
            Assert.That(
                classifications,
                Does.Contain((
                    "name",
                    SemanticClassificationKind.Property,
                    SemanticClassificationModifiers.None
                ))
            );
            Assert.That(
                classifications,
                Does.Contain((
                    "label",
                    SemanticClassificationKind.Property,
                    SemanticClassificationModifiers.Declaration
                ))
            );
        }
    }

    [Test]
    public void ClassifiesReferencesRemovedByConstantFolding()
    {
        const string Source = """
            var first = 1;
            var second = true ? first : first;
            """;
        SourceText source = SourceText.From(Source);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            Source,
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
    public void ClassifiesImplicitConversions()
    {
        const string Source = "var value: number = 1;";
        SourceText source = SourceText.From(Source);
        SemanticClassificationResult result = MuLangCompiler.ClassifySemantically(
            Source,
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
