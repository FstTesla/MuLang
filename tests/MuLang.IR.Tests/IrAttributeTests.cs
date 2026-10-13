using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR.Serialization;

namespace MuLang.IR.Tests;

public sealed class IrAttributeTests
{
    [Test]
    public void ProgramDefaultsToEmptyAttributes()
    {
        IrProgram program = CreateProgram();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(program.Attributes, Is.SameAs(IrAttributeCollection.Empty));
            Assert.That(program.Attributes.Program, Is.Null);
            Assert.That(program.Attributes.Functions, Is.Empty);
            Assert.That(program.Attributes.Slots, Is.Empty);
            Assert.That(program.Attributes.Types, Is.Empty);
        }
    }

    [Test]
    public void DiagnosticNameResolverDistinguishesMissingAndUnnamedTargets()
    {
        IrAttributeCollection attributes = new (
            new IrProgramAttributes(
                null,
                [ new IrProviderSymbolName("global.unnamed", "") ],
                [ new IrProviderSymbolName("function.unnamed", "") ]
            ),
            [
                new KeyValuePair<string, IrFunctionAttributes>(
                    "user:0",
                    new IrFunctionAttributes(null, null, null)
                ),
            ],
            [
                new KeyValuePair<IrSlotAttributeTarget, IrSlotAttributes>(
                    new IrSlotAttributeTarget("user:0", 3),
                    new IrSlotAttributes(null, null)
                ),
            ],
            [
                new KeyValuePair<TypeSymbol, IrTypeAttributes>(
                    TypeSymbols.Int,
                    new IrTypeAttributes([ ])
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                IrDiagnosticNameResolver.GetFunctionDisplayName(
                    attributes,
                    "user:0",
                    "missing-function"
                ),
                Is.EqualTo("<user:0>")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetFunctionDisplayName(
                    attributes,
                    "user:1",
                    "missing-function"
                ),
                Is.EqualTo("missing-function")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetSlotDisplayName(
                    attributes,
                    "user:0",
                    3,
                    "missing-slot"
                ),
                Is.EqualTo("%3")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetSlotDisplayName(
                    attributes,
                    "user:0",
                    4,
                    "missing-slot"
                ),
                Is.EqualTo("missing-slot")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetTypeDisplayNames(
                    attributes,
                    TypeSymbols.Int,
                    "missing-type"
                ),
                Is.EqualTo([ "<int>" ])
            );
            Assert.That(
                IrDiagnosticNameResolver.GetTypeDisplayNames(
                    attributes,
                    TypeSymbols.String,
                    "missing-type"
                ),
                Is.EqualTo([ "missing-type" ])
            );
            Assert.That(
                IrDiagnosticNameResolver.GetProviderGlobalDisplayName(
                    attributes,
                    "global.unnamed",
                    "missing-global"
                ),
                Is.EqualTo("<global.unnamed>")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetProviderGlobalDisplayName(
                    attributes,
                    "global.missing",
                    "missing-global"
                ),
                Is.EqualTo("missing-global")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetProviderFunctionDisplayName(
                    attributes,
                    "function.unnamed",
                    "missing-provider"
                ),
                Is.EqualTo("<function.unnamed>")
            );
            Assert.That(
                IrDiagnosticNameResolver.GetProviderFunctionDisplayName(
                    attributes,
                    "function.missing",
                    "missing-provider"
                ),
                Is.EqualTo("missing-provider")
            );
        }
    }

    [Test]
    public void RoundTripsKnownAttributesAndMaterializesLegacySlotName()
    {
        IrProgram program = CreateProgram() with
        {
            Attributes = new IrAttributeCollection(
                new IrProgramAttributes(
                    new Uri("https://example.com/sources/sample%20file.mu"),
                    [ new IrProviderSymbolName("global.id", "globalName") ],
                    [ new IrProviderSymbolName("function.id", "functionName") ]
                )
                {
                    LineMap = new TextLineMap(10, [ 0, 5 ]),
                },
                [
                    new KeyValuePair<string, IrFunctionAttributes>(
                        "$entry",
                        new IrFunctionAttributes("main", null, new TextSpan(1, 2))
                    ),
                ],
                [
                    new KeyValuePair<IrSlotAttributeTarget, IrSlotAttributes>(
                        new IrSlotAttributeTarget("$entry", 0),
                        new IrSlotAttributes("value", new TextSpan(3, 4))
                    ),
                ],
                [
                    new KeyValuePair<TypeSymbol, IrTypeAttributes>(
                        TypeSymbols.Int,
                        new IrTypeAttributes([ "Count", "Value" ])
                    ),
                ]
            ),
        };
        string text = MuIrWriter.WriteToString(program);
        MuIrReadResult result = MuIrReader.Read(text);
        IrProgram read = result.Program ??
            throw new AssertionException("Expected a program.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(text, Does.Contain("slot %0 local t0 mutable region"));
            Assert.That(
                text,
                Does.Contain(
                    "attribute slot entry %0 source-name { \"value\" }"
                )
            );
            Assert.That(
                text.IndexOf(
                    "provider-function-name",
                    StringComparison.Ordinal
                ),
                Is.LessThan(
                    text.IndexOf(
                        "provider-global-name",
                        StringComparison.Ordinal
                    )
                )
            );
            Assert.That(
                text.IndexOf(
                    "provider-global-name",
                    StringComparison.Ordinal
                ),
                Is.LessThan(
                    text.IndexOf(
                        "source-document",
                        StringComparison.Ordinal
                    )
                )
            );
            Assert.That(
                read.Attributes.Program?.SourceDocument,
                Is.EqualTo(
                    new Uri("https://example.com/sources/sample%20file.mu")
                )
            );
            Assert.That(
                read.Attributes.Program?.SourceDocument?.OriginalString,
                Is.EqualTo(
                    "https://example.com/sources/sample%20file.mu"
                )
            );
            Assert.That(
                read.Attributes.Program?.LineMap?.LineStarts,
                Is.EqualTo([ 0, 5 ])
            );
            Assert.That(
                read.Attributes.Functions["$entry"].SourceName,
                Is.EqualTo("main")
            );
            Assert.That(
                read.Attributes.Slots[
                    new IrSlotAttributeTarget("$entry", 0)
                ].SourceName,
                Is.EqualTo("value")
            );
#pragma warning disable CS0618
            Assert.That(read.Slots[0].Name, Is.Null);
#pragma warning restore CS0618
            Assert.That(
                read.Attributes.Types.Values.Single().SourceNames,
                Is.EqualTo([ "Count", "Value" ])
            );
        }
    }

    [Test]
    public void RejectsRelativeSourceDocument()
    {
        IrProgram relativeProgram = CreateProgram() with
        {
            Attributes = new IrAttributeCollection(
                new IrProgramAttributes(
                    new Uri("sample.mu", UriKind.Relative),
                    [ ],
                    [ ]
                ),
                [ ],
                [ ],
                [ ]
            ),
        };
        IrProgram absoluteProgram = relativeProgram with
        {
            Attributes = new IrAttributeCollection(
                relativeProgram.Attributes.Program! with
                {
                    SourceDocument = new Uri("file:///sample.mu"),
                },
                [ ],
                [ ],
                [ ]
            ),
        };
        string invalidText = MuIrWriter
            .WriteToString(absoluteProgram)
            .Replace(
                "\"file:///sample.mu\"",
                "\"sample.mu\"",
                StringComparison.Ordinal
            );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => MuIrWriter.WriteToString(relativeProgram),
                Throws.TypeOf<MuIrSerializationException>()
            );
            Assert.That(
                MuIrReader.Read(invalidText).Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Does.Contain(MuIrDiagnosticCodes.InvalidAttribute)
            );
        }
    }

    [Test]
    public void RejectsLegacyMuIrTwoSlotNames()
    {
        const string text = """
                            muir 2
                            mode program
                            environment "env"
                            profile "profile"
                            types 1
                            type t0 intrinsic int

                            entry "$entry" t0 bb0 slots 1 blocks 1 lifetime-regions 1 exception-regions 0 {
                              lifetime-region lr0 parent none entry bb0
                              slot %0 local t0 mutable "legacy" region lr0
                              block bb0 region lr0 instructions 0 {
                                term return 0 0 %0
                              }
                            }
                            users 0
                            attributes 1 {
                              attribute program future-name { "value" { nested } }
                            }
                            end
                            """;
        MuIrReadResult result = MuIrReader.Read(text);
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo([ MuIrDiagnosticCodes.InvalidAttribute ])
        );
    }

    [Test]
    public void ConvertsMuIrOneSlotNamesOnlyToAttributes()
    {
        const string text = """
                            muir 1
                            mode program
                            environment "env"
                            profile "profile"
                            types 1
                            type t0 intrinsic int

                            entry "$entry" t0 bb0 slots 1 blocks 1 {
                              slot %0 local t0 mutable "legacy"
                              block bb0 instructions 0 {
                                term return 0 0 %0
                              }
                            }
                            users 0
                            end
                            """;
        MuIrReadResult result = MuIrReader.Read(text);
        IrProgram program = result.Program ??
            throw new AssertionException("Expected a program.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                program.Attributes.Slots[
                    new IrSlotAttributeTarget("$entry", 0)
                ].SourceName,
                Is.EqualTo("legacy")
            );
#pragma warning disable CS0618
            Assert.That(program.Slots[0].Name, Is.Null);
#pragma warning restore CS0618
        }
    }

    [Test]
    public void EnforcesAttributeLimits()
    {
        string text = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            """
            attributes 1 {
              attribute program future-name { one two }
            }
            """,
            StringComparison.Ordinal
        );
        MuIrReadResult result = MuIrReader.Read(
            text,
            new MuIrReaderOptions
            {
                MaximumAttributes = 1,
                MaximumAttributesPerTarget = 1,
                MaximumProviderSymbolNameMappings = 1,
                MaximumSourceTypeNamesPerType = 1,
                MaximumUnknownAttributePayloadTokens = 1,
            }
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo([ MuIrDiagnosticCodes.LimitExceeded ])
        );
    }

    [TestCaseSource(nameof(AttributeLimitCases))]
    public void EnforcesEachAttributeLimit(
        string attributes,
        Func<MuIrReaderOptions> createOptions
    )
    {
        string text = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            attributes,
            StringComparison.Ordinal
        );
        MuIrReadResult result = MuIrReader.Read(text, createOptions());

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo([ MuIrDiagnosticCodes.LimitExceeded ])
        );
    }

    [Test]
    public void RejectsKnownAttributeOnWrongTarget()
    {
        string text = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            """
            attributes 1 {
              attribute program source-name { "invalid" }
            }
            """,
            StringComparison.Ordinal
        );
        MuIrReadResult result = MuIrReader.Read(text);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo([ MuIrDiagnosticCodes.InvalidAttribute ])
        );
    }

    [Test]
    public void RejectsDuplicateAttributesAndIgnoresLegacyApiName()
    {
        string duplicate = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            """
            attributes 2 {
              attribute function entry source-name { "first" }
              attribute function entry source-name { "second" }
            }
            """,
            StringComparison.Ordinal
        );
        MuIrReadResult duplicateResult = MuIrReader.Read(duplicate);
#pragma warning disable CS0618
        IrProgram ignoredName = CreateProgram() with
        {
            EntryFunction = CreateProgram().EntryFunction with
            {
                Slots =
                [
                    CreateProgram().Slots[0] with
                    {
                        Name = "legacy",
                    },
                ],
            },
            Attributes = new IrAttributeCollection(
                null,
                [ ],
                [
                    new KeyValuePair<IrSlotAttributeTarget, IrSlotAttributes>(
                        new IrSlotAttributeTarget("$entry", 0),
                        new IrSlotAttributes("attribute", null)
                    ),
                ],
                [ ]
            ),
        };
#pragma warning restore CS0618

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                duplicateResult.Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Is.EqualTo([ MuIrDiagnosticCodes.InvalidAttribute ])
            );
            Assert.That(
                MuIrWriter.WriteToString(ignoredName),
                Does.Not.Contain("legacy").And.Contain(
                    "source-name { \"attribute\" }"
                )
            );
        }
    }

    [Test]
    public void RejectsInvalidProviderAndTypeNamePayloads()
    {
        string provider = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            """
            attributes 2 {
              attribute program provider-global-name { "id" "first" }
              attribute program provider-global-name { "id" "second" }
            }
            """,
            StringComparison.Ordinal
        );
        string typeNames = MuIrWriter.WriteToString(CreateProgram()).Replace(
            "attributes 0 {\n}",
            """
            attributes 1 {
              attribute type t0 source-type-names { 2 ["Second", "First"] }
            }
            """,
            StringComparison.Ordinal
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                MuIrReader.Read(provider).Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Is.EqualTo([ MuIrDiagnosticCodes.InvalidAttribute ])
            );
            Assert.That(
                MuIrReader.Read(typeNames).Diagnostics.Select(
                    static diagnostic => diagnostic.Code
                ),
                Is.EqualTo([ MuIrDiagnosticCodes.InvalidAttribute ])
            );
        }
    }

    [Test]
    public void ValidatorRejectsInvalidAttributeTargets()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = CreateProgram(environment) with
        {
            Attributes = new IrAttributeCollection(
                null,
                [
                    new KeyValuePair<string, IrFunctionAttributes>(
                        "missing",
                        new IrFunctionAttributes("name", null, null)
                    ),
                ],
                [ ],
                [ ]
            ),
        };

        Assert.That(
            IrValidator.Validate(program, environment)
                .Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidAttributes)
        );
    }

    private static IrProgram CreateProgram(EnvironmentSchema? environment = null)
    {
        EnvironmentSchema schema = environment ?? new EnvironmentBuilder().Build();
        IrSlot slot = new (
            0,
            IrSlotKind.Local,
            TypeSymbols.Int,
            IrSlotMutability.Mutable,
            0
        );
        IrFunction entry = new (
            "$entry",
            TypeSymbols.Int,
            0,
            [ slot ],
            [
                new IrBasicBlock(
                    0,
                    [ ],
                    new IrTerminator.Return(default, 0),
                    0
                ),
            ],
            [ new IrLifetimeRegion(0, null, 0) ],
            [ ]
        );

        return new IrProgram(
            schema.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Latest.Fingerprint,
            entry,
            [ ]
        );
    }

    private static IEnumerable<TestCaseData> AttributeLimitCases()
    {
        yield return new TestCaseData(
            """
            attributes 2 {
              attribute program future-one { }
              attribute function entry future-two { }
            }
            """,
            static () => new MuIrReaderOptions
            {
                MaximumAttributes = 1,
            }
        ).SetName("EnforcesTotalAttributeLimit");
        yield return new TestCaseData(
            """
            attributes 2 {
              attribute program future-one { }
              attribute program future-two { }
            }
            """,
            static () => new MuIrReaderOptions
            {
                MaximumAttributesPerTarget = 1,
            }
        ).SetName("EnforcesPerTargetAttributeLimit");
        yield return new TestCaseData(
            """
            attributes 1 {
              attribute program future-name { one two }
            }
            """,
            static () => new MuIrReaderOptions
            {
                MaximumUnknownAttributePayloadTokens = 1,
            }
        ).SetName("EnforcesUnknownPayloadTokenLimit");
        yield return new TestCaseData(
            """
            attributes 1 {
              attribute type t0 source-type-names { 2 ["First", "Second"] }
            }
            """,
            static () => new MuIrReaderOptions
            {
                MaximumSourceTypeNamesPerType = 1,
            }
        ).SetName("EnforcesSourceTypeNameLimit");
        yield return new TestCaseData(
            """
            attributes 2 {
              attribute program provider-global-name { "first" "first" }
              attribute program provider-function-name { "second" "second" }
            }
            """,
            static () => new MuIrReaderOptions
            {
                MaximumProviderSymbolNameMappings = 1,
            }
        ).SetName("EnforcesProviderMappingLimit");
    }
}
