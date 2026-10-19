using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests;

public sealed class MuLangCompilerSemanticDocumentTests
{
    [Test]
    public void TracksDeclarationReadAndWriteOccurrences()
    {
        const string markedSource =
            "var |value = 1; value = 2; var copy = value;";
        (string source, int declarationOffset) = GetPosition(markedSource);
        SemanticDocumentResult result = Analyze(source);
        IReadOnlyList<SemanticOccurrence> values =
        [
            .. result.Occurrences.Where(
                occurrence =>
                    SourceText.From(source).GetText(occurrence.Span) ==
                    "value"
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                values.Select(static occurrence => occurrence.Role),
                Is.EqualTo(
                    [
                        SemanticOccurrenceRole.Declaration,
                        SemanticOccurrenceRole.Write,
                        SemanticOccurrenceRole.Read,
                    ]
                )
            );
            Assert.That(
                values.Select(static occurrence => occurrence.Symbol),
                Is.All.EqualTo(values[0].Symbol)
            );
            Assert.That(
                values.Select(static occurrence => occurrence.DefinitionSpan),
                Is.All.EqualTo(new TextSpan(declarationOffset, 5))
            );
        }
    }

    [Test]
    public void DistinguishesShadowedLocals()
    {
        const string source = """
                              var value = 1;
                              {
                                  var value = 2;
                                  value = 3;
                              }
                              value = 4;
                              """;
        SemanticDocumentResult result = Analyze(source);
        IReadOnlyList<SemanticOccurrence> values =
        [
            .. result.Occurrences.Where(
                occurrence =>
                    SourceText.From(source).GetText(occurrence.Span) ==
                    "value"
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values, Has.Count.EqualTo(4));
            Assert.That(values[0].Symbol, Is.EqualTo(values[3].Symbol));
            Assert.That(values[1].Symbol, Is.EqualTo(values[2].Symbol));
            Assert.That(values[0].Symbol, Is.Not.EqualTo(values[1].Symbol));
        }
    }

    [Test]
    public void ResolvesPropertiesByOwnerIdentity()
    {
        const string source = """
                              type First { value: int };
                              type Second { value: int };
                              var first: First = { value = 1 };
                              var second: Second = { value = 2 };
                              var firstValue = first.value;
                              var secondValue = second.value;
                              """;
        SemanticDocumentResult result = Analyze(source);
        SourceText sourceText = SourceText.From(source);
        IReadOnlyList<SemanticOccurrence> properties =
        [
            .. result.Occurrences.Where(
                static occurrence =>
                    occurrence.Kind ==
                    SemanticClassificationKind.Property
            ),
        ];
        SemanticOccurrence firstDeclaration = properties[0];
        SemanticOccurrence secondDeclaration = properties[1];
        SemanticOccurrence firstAccess = properties[^2];
        SemanticOccurrence secondAccess = properties[^1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                properties.Select(
                    occurrence => sourceText.GetText(occurrence.Span)
                ),
                Is.All.EqualTo("value")
            );
            Assert.That(
                firstAccess.Symbol,
                Is.EqualTo(firstDeclaration.Symbol)
            );
            Assert.That(
                secondAccess.Symbol,
                Is.EqualTo(secondDeclaration.Symbol)
            );
            Assert.That(
                firstAccess.Symbol,
                Is.Not.EqualTo(secondAccess.Symbol)
            );
            Assert.That(properties[2].Symbol, Is.Not.EqualTo(firstAccess.Symbol));
            Assert.That(properties[3].Symbol, Is.Not.EqualTo(secondAccess.Symbol));
        }
    }

    [Test]
    public void ClassifiesLiteralPropertySelectorsAndTests()
    {
        const string source = """
                              var value = { item = 1 };
                              var selected = value["item"];
                              var present = value has "item";
                              """;
        SemanticDocumentResult result = Analyze(source);
        SourceText sourceText = SourceText.From(source);
        IReadOnlyList<SemanticOccurrence> properties =
        [
            .. result.Occurrences.Where(
                static occurrence =>
                    occurrence.Kind ==
                    SemanticClassificationKind.Property
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                properties.Select(
                    occurrence => sourceText.GetText(occurrence.Span)
                ),
                Is.EqualTo(["item", "\"item\"", "\"item\""])
            );
            Assert.That(
                properties.Select(static occurrence => occurrence.Symbol),
                Is.All.EqualTo(properties[0].Symbol)
            );
            Assert.That(
                properties.Skip(1).Select(
                    static occurrence => occurrence.Role
                ),
                Is.All.EqualTo(SemanticOccurrenceRole.Read)
            );
        }
    }

    [Test]
    public void ExcludesDynamicPropertiesFromNavigationIdentity()
    {
        SemanticDocumentResult result = Analyze(
            "var value = @{ dynamic? = 1 }; value.missing;"
        );
        SemanticOccurrence missing = result.Occurrences.Single(
            occurrence =>
                occurrence.Kind == SemanticClassificationKind.Property &&
                occurrence.HoverContent ==
                "[property] missing: unknown?"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing.Symbol, Is.Null);
            Assert.That(missing.IsClassification, Is.True);
        }
    }

    [Test]
    public void ExposesUnresolvedNamesOnlyForHover()
    {
        const string source = "var value = missing;";
        SemanticDocumentResult result = Analyze(source);
        SemanticOccurrence missing = result.Occurrences.Single(
            static occurrence =>
                occurrence.HoverContent ==
                "[variable] missing: <unresolved>"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing.Symbol, Is.Null);
            Assert.That(missing.IsClassification, Is.False);
            Assert.That(missing.DefinitionSpan, Is.Null);
            Assert.That(
                result.GetOccurrences(
                    result.Occurrences.Single(
                        static occurrence =>
                            occurrence.Symbol is not null
                    ).Symbol!
                ),
                Has.Count.EqualTo(1)
            );
        }
    }

    [Test]
    public void MatchesConfiguredSymbolsAcrossIndependentDocuments()
    {
        ObjectTypeSymbol itemType = new (
            "item",
            "Item",
            false,
            [ new ObjectPropertySymbol("name", TypeSymbols.String) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("current", "current", itemType)
            .AddFunction(
                "load",
                "load",
                [ new ParameterSymbol("item", itemType) ],
                itemType
            )
            .Build();
        SemanticDocumentResult first = Analyze(
            "load(current).name",
            environment,
            CompilationMode.Expression
        );
        SemanticDocumentResult second = Analyze(
            "current.name",
            environment,
            CompilationMode.Expression
        );

        foreach (
            SemanticClassificationKind kind in new[]
            {
                SemanticClassificationKind.Variable,
                SemanticClassificationKind.Property,
            }
        )
        {
            SemanticSymbolKey firstKey = first.Occurrences
                .Single(occurrence => occurrence.Kind == kind)
                .Symbol!;
            SemanticSymbolKey secondKey = second.Occurrences
                .Single(occurrence => occurrence.Kind == kind)
                .Symbol!;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstKey.IsWorkspaceStable, Is.True);
                Assert.That(firstKey, Is.EqualTo(secondKey));
            }
        }
    }

    [Test]
    public void MatchesIntrinsicTypesAcrossIndependentDocuments()
    {
        const string firstSource = "var value: int = 1;";
        const string secondSource =
            "func identity(value: int): int { return value; }";
        SemanticDocumentResult first = Analyze(firstSource);
        SemanticDocumentResult second = Analyze(secondSource);
        SemanticSymbolKey firstKey = first.Occurrences.Single(
            occurrence =>
                occurrence.Kind == SemanticClassificationKind.Type &&
                SourceText.From(firstSource).GetText(occurrence.Span) ==
                "int"
        ).Symbol!;
        IReadOnlyList<SemanticSymbolKey> secondKeys =
        [
            .. second.Occurrences
                .Where(
                    occurrence =>
                        occurrence.Kind ==
                        SemanticClassificationKind.Type
                )
                .Select(static occurrence => occurrence.Symbol!),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstKey.IsWorkspaceStable, Is.True);
            Assert.That(secondKeys, Has.Count.EqualTo(2));
            Assert.That(secondKeys, Is.All.EqualTo(firstKey));
        }
    }

    [Test]
    public void UnifiesThrowPrototypeAndErrorProperties()
    {
        const string source = """
                              func run(): void {
                                  try {
                                      throw {
                                          message = "outer",
                                          cause = {
                                              message = "inner",
                                          },
                                      };
                                  } catch (failure) {
                                      var text = failure.message;
                                  }
                              }
                              """;
        SourceText sourceText = SourceText.From(source);
        SemanticDocumentResult result = Analyze(source);
        IReadOnlyList<SemanticOccurrence> messages =
        [
            .. result.Occurrences.Where(
                occurrence =>
                    occurrence.Kind ==
                        SemanticClassificationKind.Property &&
                    sourceText.GetText(occurrence.Span) == "message"
            ),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Has.Count.EqualTo(3));
            Assert.That(
                messages.Select(static occurrence => occurrence.Symbol),
                Is.All.EqualTo(messages[0].Symbol)
            );
            Assert.That(messages[0].Symbol!.IsWorkspaceStable, Is.True);
            Assert.That(
                messages.Select(static occurrence => occurrence.Role),
                Is.EqualTo(
                    [
                        SemanticOccurrenceRole.Write,
                        SemanticOccurrenceRole.Write,
                        SemanticOccurrenceRole.Read,
                    ]
                )
            );
        }
    }

    [Test]
    public void GroupsAssociatedControlFlowKeywords()
    {
        const string source =
            "if (true) { } else if (false) { } else { }";
        SemanticDocumentResult result = Analyze(source);
        IReadOnlyList<SemanticOccurrence> highlights =
            result.GetHighlights(
                source.IndexOf("if", StringComparison.Ordinal)
            );
        SourceText sourceText = SourceText.From(source);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(highlights, Has.Count.EqualTo(4));
            Assert.That(
                highlights.Select(
                    occurrence =>
                        sourceText.GetText(occurrence.Span)
                ),
                Is.EqualTo(["if", "else", "if", "else"])
            );
            Assert.That(
                highlights,
                Has.All.Matches<SemanticOccurrence>(
                    static occurrence =>
                        occurrence.Symbol is null &&
                        occurrence.HoverItems.Count == 0
                )
            );
        }
    }

    [Test]
    public void TreatsLegacyConfiguredPropertyInitializersAsWrites()
    {
        ObjectTypeSymbol itemType = new (
            "item",
            "Item",
            false,
            [ new ObjectPropertySymbol("name", TypeSymbols.String) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .Build();
        LanguageProfile profile = new LanguageProfileBuilder(
            LanguageProfiles.Version1_2
        )
            .WithObjectLiteralSyntax(ObjectLiteralSyntax.Legacy)
            .Build();
        SemanticDocumentResult result =
            MuLangCompiler.GetSemanticDocument(
                "{ name: \"value\" }",
                environment,
                CompilationMode.Expression,
                new MuLangCompilationOptions
                {
                    ExpectedResultType = itemType,
                    Profile = profile,
                }
            );
        SemanticOccurrence property = result.Occurrences.Single(
            static occurrence =>
                occurrence.Kind ==
                SemanticClassificationKind.Property
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                property.Role,
                Is.EqualTo(SemanticOccurrenceRole.Write)
            );
            Assert.That(property.DefinitionSpan, Is.Null);
            Assert.That(property.Symbol!.IsWorkspaceStable, Is.True);
        }
    }

    [Test]
    public void ResolvesForwardDefinitions()
    {
        const string markedSource = """
                                    func first(value: |Node): Node {
                                        return second(value);
                                    }
                                    type Node { next?: Node };
                                    func second(value: Node): Node {
                                        return value;
                                    }
                                    """;
        (string source, int nodeReference) = GetPosition(markedSource);
        SemanticDocumentResult result = Analyze(source);
        SemanticOccurrence typeReference =
            result.GetOccurrence(nodeReference)!;
        int functionReference = source.IndexOf(
            "second(value)",
            StringComparison.Ordinal
        );
        SemanticOccurrence function =
            result.GetOccurrence(functionReference)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                SourceText.From(source).GetText(
                    typeReference.DefinitionSpan!.Value
                ),
                Is.EqualTo("Node")
            );
            Assert.That(
                SourceText.From(source).GetText(
                    function.DefinitionSpan!.Value
                ),
                Is.EqualTo("second")
            );
        }
    }

    private static SemanticDocumentResult Analyze(
        string source,
        EnvironmentSchema? environment = null,
        CompilationMode compilationMode = CompilationMode.Program
    )
    {
        return MuLangCompiler.GetSemanticDocument(
            source,
            environment ?? new EnvironmentBuilder().Build(),
            compilationMode
        );
    }

    private static (string Source, int Offset) GetPosition(string markedSource)
    {
        int marker = markedSource.IndexOf('|');
        return (
            markedSource.Remove(marker, 1),
            SourceText.From(markedSource[..marker]).Length
        );
    }
}
