using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Exporters.DotNet.Tests.Exporting;

public sealed class DotNetExporterTests
{
    [Test]
    public void ExecutesArithmeticExpression()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1 + 2 * 3",
            environment
        );
        DotNetRuntimeContext context = CreateContext(environment);

        Assert.That(compiled(context), Is.EqualTo(7L));
    }

    [Test]
    public void ResolvesGlobalsAndProviderFunctionsById()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .AddFunction(
                "function.increment",
                "increment",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.Int
            )
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "increment(value)",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 41L) ],
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.increment",
                    static arguments =>
                        (long)(arguments[0] ??
                            throw new AssertionException("Expected an argument.")) + 1
                ),
            ]
        );

        Assert.That(compiled(context), Is.EqualTo(42L));
    }

    [Test]
    public void PreservesShortCircuitEvaluation()
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Bool)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "false && touch()",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.touch",
                    _ =>
                    {
                        invocationCount++;
                        return true;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.False);
            Assert.That(invocationCount, Is.Zero);
        }
    }

    [Test]
    public void NullCoalescingEvaluatesFallbackOnlyForNull()
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .AddFunction("function.fallback", "fallback", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "value ?? fallback()",
            environment
        );
        IEnumerable<KeyValuePair<string, DotNetFunction>> functions =
        [
            new (
                "function.fallback",
                _ =>
                {
                    invocationCount++;
                    return 42L;
                }
            ),
        ];
        DotNetRuntimeContext nonNullContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 7L) ],
            functions
        );
        DotNetRuntimeContext nullContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", null) ],
            functions
        );

        object? nonNullResult = compiled(nonNullContext);
        object? nullResult = compiled(nullContext);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nonNullResult, Is.EqualTo(7L));
            Assert.That(nullResult, Is.EqualTo(42L));
            Assert.That(invocationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void NullCoalescingEvaluatesRightOperandForNullLiteral()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "null ?? 42",
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(42L));
    }

    [TestCase(1L, 1.0)]
    [TestCase(null, 2.5)]
    public void NullCoalescingConvertsToTheCommonNumericType(
        object? value,
        double expected
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.value",
                "value",
                TypeSymbols.Nullable(TypeSymbols.Int)
            )
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "value ?? 2.5",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", value) ]
        );

        Assert.That(compiled(context), Is.EqualTo(expected));
    }

    [TestCase("false & touch()", false)]
    [TestCase("true | touch()", true)]
    [TestCase("true ^ touch()", false)]
    public void BooleanEagerOperatorsEvaluateBothOperands(
        string source,
        bool expected
    )
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Bool)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.touch",
                    _ =>
                    {
                        invocationCount++;
                        return true;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.EqualTo(expected));
            Assert.That(invocationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExecutesForLoopAndLocalAssignments()
    {
        const string source = """
                              var sum = 0;
                              for (var index = 0; index < 5; index = index + 1)
                                  sum = sum + index;
                              return sum;
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(10L));
    }

    [Test]
    public void BreakCanExitMultipleNestedLoops()
    {
        const string source = """
                              var count = 0;
                              for (var outer = 0; outer < 3; outer = outer + 1) {
                                  for (var inner = 0; inner < 3; inner = inner + 1) {
                                      count = count + 1;
                                      break 2;
                                  }
                              }
                              return count;
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(1L));
    }

    [Test]
    public void ContinueCanAdvanceAnOuterLoop()
    {
        const string source = """
                              var count = 0;
                              for (var outer = 0; outer < 3; outer = outer + 1) {
                                  for (var inner = 0; inner < 3; inner = inner + 1) {
                                      count = count + 1;
                                      continue 2;
                                  }
                              }
                              return count;
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(3L));
    }

    [Test]
    public void ExecutesArrayCreationMutationIndexingAndLength()
    {
        const string source = """
                              var values: int[] = [1, 2];
                              values[0] = 3;
                              return values.length + values[0];
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(5L));
    }

    [Test]
    public void ExecutesReadOnlyArrayLiteralReadsAndLength()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "$[1, 2].length + $[3][0]",
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(5L));
    }

    [Test]
    public void ExecutesNestedReadOnlyArraysAndReadOnlyReturns()
    {
        const string source = """
                              func values(): int[]$ {
                                  return $[1];
                              }
                              return $[values()][0][0];
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(1L));
    }

    [Test]
    public void PreservesIdentityAcrossReadOnlyViews()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "[1] === ([1] as int[]$)",
            environment,
            TypeSymbols.Bool
        );
        MutableArrayValue value = new ([ 1L ]);
        EnvironmentSchema globalEnvironment = new EnvironmentBuilder()
            .AddGlobal("global.values", "values", TypeSymbols.Array(TypeSymbols.Int))
            .Build(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> globalCompiled = CompileExpression(
            "values === (values as int[]$)",
            globalEnvironment,
            TypeSymbols.Bool
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(CreateContext(environment)), Is.False);
            Assert.That(
                globalCompiled(
                    CreateContext(
                        globalEnvironment,
                        [ new KeyValuePair<string, object?>("global.values", value) ]
                    )
                ),
                Is.True
            );
        }
    }

    [Test]
    public void MutableAliasUpdatesReadOnlyView()
    {
        const string source = """
                              var mutable = [1];
                              var view: int[]$ = mutable;
                              mutable[0] = 2;
                              return view[0];
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(2L));
    }

    [Test]
    public void CheckedMutableCapabilityAcquisitionUsesRuntimeCapability()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> mutableCast = CompileExpression(
            "[1] as int[]$ as int[]",
            environment,
            TypeSymbols.Array(TypeSymbols.Int)
        );
        Func<DotNetRuntimeContext, object?> readOnlyLiteralCast = CompileExpression(
            "$[1] as int[]",
            environment,
            TypeSymbols.Array(TypeSymbols.Int)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                mutableCast(CreateContext(environment)),
                Is.InstanceOf<IDotNetArrayValue>()
            );
            MuLangRuntimeException exception = RequireRuntimeException(
                () => readOnlyLiteralCast(CreateContext(environment))
            );
            Assert.That(exception.Code, Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion));
        }
    }

    [Test]
    public void AcceptsGenuinelyReadOnlyProviderArrays()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.ReadOnlyArray(TypeSymbols.Int)
            )
            .Build(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values[0]",
            environment,
            TypeSymbols.Int
        );
        ReadOnlyArrayValue values = new ([ 7L ]);

        Assert.That(
            compiled(
                CreateContext(
                    environment,
                    [ new KeyValuePair<string, object?>("global.values", values) ]
                )
            ),
            Is.EqualTo(7L)
        );
    }

    [Test]
    public void ArrayTypeTestsInspectRuntimeCapabilityAndShape()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.ReadOnlyArray(TypeSymbols.Unknown)
            )
            .Build(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> mutableTest = CompileExpression(
            "values is int[]",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> readOnlyTest = CompileExpression(
            "values is int[]$",
            environment,
            TypeSymbols.Bool
        );
        ReadOnlyArrayValue values = new ([ 1L ]);
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mutableTest(context), Is.False);
            Assert.That(readOnlyTest(context), Is.True);
        }
    }

    [Test]
    public void PassesOriginalMutableArrayToTrustedReadOnlyProvider()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.values", "values", TypeSymbols.Array(TypeSymbols.Int))
            .AddFunction(
                "function.inspect",
                "inspect",
                [
                    new ParameterSymbol(
                        "first",
                        TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
                    ),
                    new ParameterSymbol(
                        "second",
                        TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
                    ),
                ],
                TypeSymbols.Bool
            )
            .Build(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "inspect(values, values)",
            environment,
            TypeSymbols.Bool
        );
        MutableArrayValue values = new ([ 1L ]);
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ],
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.inspect",
                    arguments =>
                        arguments[0] is IDotNetArrayValue first &&
                        ReferenceEquals(arguments[0], values) &&
                        ReferenceEquals(arguments[0], arguments[1]) &&
                        first.TrySetElement(0, 2L)
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.True);
            Assert.That(values.TryGetElement(0, out object? value), Is.True);
            Assert.That(value, Is.EqualTo(2L));
        }
    }

    [Test]
    public void ElementReadsDetectShapeChangesThroughMutableAliases()
    {
        const string source = """
                              var view = values as int[]$;
                              mutate();
                              return view[0];
                              """;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Unknown)
            )
            .AddFunction("function.mutate", "mutate", [ ], TypeSymbols.Void)
            .Build(LanguageVersion.Version1_1);
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );
        MutableArrayValue values = new ([ 1L ]);
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ],
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.mutate",
                    _ =>
                    {
                        values.TrySetElement(0, "invalid");
                        return null;
                    }
                ),
            ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo(DotNetRuntimeErrorCodes.InvalidRuntimeValue));
    }

    [Test]
    public void ExecutesDynamicObjectMutationRemovalAndHas()
    {
        const string source = """
                              var item = @{ value: 1 };
                              item.extra = 2;
                              item.extra~;
                              return item has "extra";
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.False);
    }

    [Test]
    public void DynamicPropertyCanBePresentWithNullValue()
    {
        const string source = """
                              var item = @{ };
                              item.value = null;
                              return item has "value";
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.True);
    }

    [Test]
    public void ExecutesDynamicPropertyTestWithPropertyExistenceOnly()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.item", "item", TypeSymbols.Object)
            .AddGlobal("global.key", "key", TypeSymbols.String)
            .Build();
        LanguageProfile profile = new LanguageProfileBuilder()
            .WithOpenObjects(OpenObjectsFeature.PropertyExistenceOnly)
            .Build();
        CompilationResult result = MuLangCompiler.Compile(
            "item has key",
            environment,
            CompilationMode.Expression,
            TypeSymbols.Bool,
            profile
        );
        DotNetExportResult export = DotNetExporter.Export(
            result.Program ?? throw new AssertionException("Expected compilation to produce an IR program."),
            environment,
            CompilationMode.Expression,
            profile.Fingerprint
        );
        Func<DotNetRuntimeContext, object?> compiled = export.Delegate ??
            throw new AssertionException("Expected a compiled delegate.");
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>(
                    "global.item",
                    new MutableObjectValue(
                        [ new KeyValuePair<string, object?>("value", 1L) ]
                    )
                ),
                new KeyValuePair<string, object?>("global.key", "value"),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(export.Diagnostics, Is.Empty);
            Assert.That(compiled(context), Is.True);
        }
    }

    [Test]
    public void ExecutesRemovalOfInferredOptionalProperty()
    {
        const string source = """
                              var item = { value?: 1 };
                              item.value~;
                              return item has "value";
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.False);
    }

    [Test]
    public void DistinguishesStructuralEqualityAndIdentity()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> structural = CompileExpression(
            "{ value: 1 } == { value: 1 }",
            environment
        );
        Func<DotNetRuntimeContext, object?> identity = CompileExpression(
            "{ value: 1 } === { value: 1 }",
            environment
        );
        DotNetRuntimeContext structuralContext = CreateContext(environment);
        DotNetRuntimeContext identityContext = CreateContext(environment);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(structural(structuralContext), Is.True);
            Assert.That(identity(identityContext), Is.False);
        }
    }

    [Test]
    public void ExecutesOptionalAccessOnNull()
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
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "item?.value",
            environment,
            TypeSymbols.Nullable(TypeSymbols.Int)
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.item", null) ]
        );

        Assert.That(compiled(context), Is.Null);
    }

    [Test]
    public void OptionalElementAccessSkipsIndexEvaluationForNullTarget()
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Nullable(TypeSymbols.Array(TypeSymbols.Int))
            )
            .AddFunction("function.index", "index", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values?.[index()]",
            environment,
            TypeSymbols.Nullable(TypeSymbols.Int)
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", null) ],
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.index",
                    _ =>
                    {
                        invocationCount++;
                        return 0L;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.Null);
            Assert.That(invocationCount, Is.Zero);
        }
    }

    [Test]
    public void WrapsMinimumIntegerRemainderOverflow()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.left", "left", TypeSymbols.Int)
            .AddGlobal("global.right", "right", TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "left % right",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>(
                    "global.left",
                    long.MinValue
                ),
                new KeyValuePair<string, object?>("global.right", -1L),
            ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6003"));
    }

    [Test]
    public void StructuralEqualityPromotesIntToFloat()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "9007199254740993 == 9007199254740992.0",
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.True);
    }

    [Test]
    public void IdentityEqualityDistinguishesIntAndFloat()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1 === 1.0",
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.False);
    }

    [Test]
    public void GenericNumberArithmeticPreservesRuntimeKinds()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.left", "left", TypeSymbols.Number)
            .AddGlobal("global.right", "right", TypeSymbols.Number)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "left + right",
            environment,
            TypeSymbols.Number
        );
        DotNetRuntimeContext integerContext = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>("global.left", 1L),
                new KeyValuePair<string, object?>("global.right", 2L),
            ]
        );
        DotNetRuntimeContext mixedContext = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>("global.left", 1L),
                new KeyValuePair<string, object?>("global.right", 2.5),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(integerContext), Is.EqualTo(3L));
            Assert.That(compiled(mixedContext), Is.EqualTo(3.5));
        }
    }

    [Test]
    public void CheckedNumberCastsRequireTheRuntimeNumericKind()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        Func<DotNetRuntimeContext, object?> toInt = CompileExpression(
            "value as int",
            environment,
            TypeSymbols.Int
        );
        Func<DotNetRuntimeContext, object?> toFloat = CompileExpression(
            "value as float",
            environment,
            TypeSymbols.Float
        );
        DotNetRuntimeContext integerContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 2L) ]
        );
        DotNetRuntimeContext floatContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 2.0) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(toInt(integerContext), Is.EqualTo(2L));
            Assert.That(toFloat(floatContext), Is.EqualTo(2.0));
            Assert.That(
                RequireRuntimeException(() => toFloat(integerContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
            Assert.That(
                RequireRuntimeException(() => toInt(floatContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
        }
    }

    [Test]
    public void TypeTestsUseRuntimeNumericRepresentation()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        Func<DotNetRuntimeContext, object?> intIsFloat = CompileExpression(
            "value is float",
            environment
        );
        Func<DotNetRuntimeContext, object?> floatIsInt = CompileExpression(
            "value is int",
            environment
        );
        Func<DotNetRuntimeContext, object?> intIsNumber = CompileExpression(
            "value is number",
            environment
        );
        DotNetRuntimeContext integerContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 1L) ]
        );
        DotNetRuntimeContext floatContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 1.0) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(intIsFloat(integerContext), Is.False);
            Assert.That(floatIsInt(floatContext), Is.False);
            Assert.That(intIsNumber(integerContext), Is.True);
        }
    }

    [Test]
    public void CheckedCastsSucceedExactlyWhenTypeTestsDo()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Unknown)
            .Build();
        Func<DotNetRuntimeContext, object?> isInt = CompileExpression(
            "value is int",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> asInt = CompileExpression(
            "value as int",
            environment,
            TypeSymbols.Int
        );
        Func<DotNetRuntimeContext, object?> isFloat = CompileExpression(
            "value is float",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> asFloat = CompileExpression(
            "value as float",
            environment,
            TypeSymbols.Float
        );
        Func<DotNetRuntimeContext, object?> isString = CompileExpression(
            "value is string",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> asString = CompileExpression(
            "value as string",
            environment,
            TypeSymbols.String
        );
        Func<DotNetRuntimeContext, object?> isObject = CompileExpression(
            "value is object",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> asObject = CompileExpression(
            "value as object",
            environment,
            TypeSymbols.Object
        );
        DotNetRuntimeContext integerContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 2L) ]
        );
        DotNetRuntimeContext floatContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 2.0) ]
        );
        DotNetRuntimeContext stringContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", "value") ]
        );
        MutableObjectValue objectValue = new (
            [ new KeyValuePair<string, object?>("value", 2L) ]
        );
        DotNetRuntimeContext objectContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", objectValue) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isInt(integerContext), Is.True);
            Assert.That(asInt(integerContext), Is.EqualTo(2L));
            Assert.That(isFloat(integerContext), Is.False);
            Assert.That(
                RequireRuntimeException(() => asFloat(integerContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
            Assert.That(isFloat(floatContext), Is.True);
            Assert.That(asFloat(floatContext), Is.EqualTo(2.0));
            Assert.That(isInt(floatContext), Is.False);
            Assert.That(
                RequireRuntimeException(() => asInt(floatContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
            Assert.That(isString(stringContext), Is.True);
            Assert.That(asString(stringContext), Is.EqualTo("value"));
            Assert.That(isString(integerContext), Is.False);
            Assert.That(
                RequireRuntimeException(() => asString(integerContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
            Assert.That(isObject(objectContext), Is.True);
            Assert.That(asObject(objectContext), Is.SameAs(objectValue));
            Assert.That(isObject(stringContext), Is.False);
            Assert.That(
                RequireRuntimeException(() => asObject(stringContext)).Code,
                Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion)
            );
        }
    }

    [Test]
    public void RuntimeBoundariesDistinguishFloatFromNumber()
    {
        EnvironmentSchema floatEnvironment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Float)
            .Build();
        EnvironmentSchema numberEnvironment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        Func<DotNetRuntimeContext, object?> readFloat = CompileExpression(
            "value",
            floatEnvironment
        );
        Func<DotNetRuntimeContext, object?> readNumber = CompileExpression(
            "value",
            numberEnvironment
        );
        DotNetRuntimeContext invalidFloatContext = CreateContext(
            floatEnvironment,
            [ new KeyValuePair<string, object?>("global.value", 1L) ]
        );
        DotNetRuntimeContext integerNumberContext = CreateContext(
            numberEnvironment,
            [ new KeyValuePair<string, object?>("global.value", 1L) ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                RequireRuntimeException(() => readFloat(invalidFloatContext)).Code,
                Is.EqualTo("MUL6015")
            );
            Assert.That(readNumber(integerNumberContext), Is.EqualTo(1L));
        }
    }

    [Test]
    public void UnknownCastsUseRuntimeConformance()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Unknown)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "value as int",
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 2.0) ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo(DotNetRuntimeErrorCodes.InvalidConversion));
    }

    [TestCase("==")]
    [TestCase("===")]
    public void NanIsNotEqualToItself(string equalityOperator)
    {
        string source = $"""
                         var value = 0.0 / 0.0;
                         return value {equalityOperator} value;
                         """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.False);
    }

    [Test]
    public void FormatsNumberConcatenationAsNumberLiteral()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "\"\" + 1.0",
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo("1.0"));
    }

    [Test]
    public void RejectsClrArraysWithoutAnAdapter()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Int)
            )
            .Build();
        long[] values = [ 42L ];
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values[0]",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void AcceptsArrayAdapters()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Int)
            )
            .Build();
        MutableArrayValue values = new ([ 42L ]);
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            "values[0] = 7; return values[0];",
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ]
        );

        Assert.That(compiled(context), Is.EqualTo(7L));
    }

    [Test]
    public void RejectsClrDictionariesWithoutAnAdapter()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.item", "item", TypeSymbols.Object)
            .Build();
        object item = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = 1L,
        };
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "item",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.item", item) ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void EnforcesExecutionBudget()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            executionBudget: 0
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6011"));
    }

    [Test]
    public void StaticallyGuaranteedCastDoesNotConsumeAdditionalBudget()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1 as int",
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            executionBudget: 2
        );

        Assert.That(compiled(context), Is.EqualTo(1L));
    }

    [Test]
    public void EnforcesCancellation()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1",
            environment
        );
        using CancellationTokenSource cts = new ();
        cts.Cancel();
        DotNetRuntimeContext context = CreateContext(
            environment,
            cancellationToken: cts.Token
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6016"));
    }

    [Test]
    public void RejectsIncompatibleRuntimeEnvironment()
    {
        EnvironmentSchema compiledEnvironment = CreateEmptyEnvironment();
        EnvironmentSchema runtimeEnvironment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1",
            compiledEnvironment
        );
        DotNetRuntimeContext context = CreateContext(runtimeEnvironment);

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6010"));
    }

    [Test]
    public void RejectsInvalidGlobalRuntimeValues()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "value",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", 1) ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void RejectsInvalidProviderReturnValues()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.value", "value", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "value()",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.value",
                    static _ => "invalid"
                ),
            ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void DeeplyValidatesGlobalValuesAtTheBoundary()
    {
        ObjectTypeSymbol containerType = new (
            "type.container",
            "Container",
            false,
            [
                new ObjectPropertySymbol(
                    "values",
                    TypeSymbols.Array(TypeSymbols.Int)
                ),
            ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(containerType)
            .AddGlobal("global.container", "container", containerType)
            .Build();
        MutableObjectValue container = new (
            [
                new KeyValuePair<string, object?>(
                    "values",
                    new MutableArrayValue([ "invalid" ])
                ),
            ]
        );
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "container",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.container", container) ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void AllowsNullGenericObjectProperties()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.item", "item", TypeSymbols.Object)
            .Build();
        MutableObjectValue item = new (
            [ new KeyValuePair<string, object?>("value", null) ]
        );
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "item has \"value\"",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.item", item) ]
        );

        Assert.That(compiled(context), Is.True);
    }

    [Test]
    public void EnforcesConfigurableTraversalDepth()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Array(TypeSymbols.Int))
            )
            .Build();
        MutableArrayValue values = new (
            [ new MutableArrayValue([ 1L ]) ]
        );
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ],
            maximumTraversalDepth: 0
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
    }

    [Test]
    public void CyclicObjectConformanceTracksRuntimeIdentity()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [
                new ObjectPropertySymbol("self", TypeSymbols.Object),
                new ObjectPropertySymbol("label", TypeSymbols.String),
            ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.value", "value", TypeSymbols.Unknown)
            .Build();
        Func<DotNetRuntimeContext, object?> typeTest = CompileExpression(
            "value is Item",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> cast = CompileExpression(
            "value as Item",
            environment,
            itemType
        );
        MutableObjectValue value = new (
            [ new KeyValuePair<string, object?>("label", "value") ]
        );
        value.Set("self", value);
        DotNetRuntimeContext typeTestContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", value) ],
            maximumTraversalDepth: 2
        );
        DotNetRuntimeContext castContext = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", value) ],
            maximumTraversalDepth: 2
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeTest(typeTestContext), Is.True);
            Assert.That(cast(castContext), Is.SameAs(value));
        }
    }

    [Test]
    public void PreservesProviderCancellation()
    {
        using CancellationTokenSource cts = new ();
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.cancel", "cancel", [ ], TypeSymbols.Void)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            "cancel();",
            environment,
            TypeSymbols.Void
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.cancel",
                    _ =>
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        cts.Cancel();
                        return null;
                    }
                ),
            ],
            cancellationToken: cts.Token
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6016"));
    }

    [Test]
    public void AcceptsAbsentOptionalPropertiesOnClosedProviderObjects()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [
                new ObjectPropertySymbol("id", TypeSymbols.Int),
                new ObjectPropertySymbol("label", TypeSymbols.String, true),
            ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.item", "item", itemType)
            .Build();
        MutableObjectValue item = new (
            [ new KeyValuePair<string, object?>("id", 42L) ]
        );
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "item.id",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.item", item) ]
        );

        Assert.That(compiled(context), Is.EqualTo(42L));
    }

    [Test]
    public void SurfacesRejectedProviderMutations()
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
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            "item.value = 2;",
            environment,
            TypeSymbols.Void
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>(
                    "global.item",
                    new ReadOnlyObjectValue("value", 1L)
                ),
            ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6008"));
    }

    [Test]
    public void StructuralEqualityHandlesCyclicObjects()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.left", "left", TypeSymbols.Object)
            .AddGlobal("global.right", "right", TypeSymbols.Object)
            .Build();
        MutableObjectValue left = new ([ ]);
        MutableObjectValue right = new ([ ]);
        left.Set("self", left);
        right.Set("self", right);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "left == right",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>("global.left", left),
                new KeyValuePair<string, object?>("global.right", right),
            ]
        );

        Assert.That(compiled(context), Is.True);
    }

    [Test]
    public void ChargesDeepStructuralEqualityToTheBudget()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.left", "left", TypeSymbols.Object)
            .AddGlobal("global.right", "right", TypeSymbols.Object)
            .Build();
        object left = CreateNestedObject(20);
        object right = CreateNestedObject(20);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "left == right",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>("global.left", left),
                new KeyValuePair<string, object?>("global.right", right),
            ],
            executionBudget: 10
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6011"));
    }

    [Test]
    public void BudgetTerminatesInfinitePrograms()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            "while (true) { }",
            environment,
            TypeSymbols.Void
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            executionBudget: 10
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6011"));
    }

    [Test]
    public void LowersShortCircuitToExplicitControlFlow()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.flag", "flag", TypeSymbols.Bool)
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Bool)
            .Build();
        IrProgram program = LowerExpression("flag || touch()", environment);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(program.Blocks, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(
                program.Blocks.Select(static block => block.Terminator),
                Has.Some.TypeOf<IrTerminator.Branch>()
            );
            Assert.That(IrValidator.Validate(program, environment), Is.Empty);
        }
    }

    [Test]
    public void PreservesAssignmentEvaluationOrderInIr()
    {
        ObjectTypeSymbol itemType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(itemType)
            .AddFunction("function.target", "target", [ ], itemType)
            .AddFunction("function.value", "value", [ ], TypeSymbols.Int)
            .Build();
        IrProgram program = LowerProgram(
            "target().value = value();",
            environment,
            TypeSymbols.Void
        );
        IEnumerable<string> callIds =
        [
            .. program.Blocks
                .SelectMany(static block => block.Instructions)
                .OfType<IrInstruction.ProviderCall>()
                .Select(static call => call.FunctionId),
        ];

        Assert.That(
            callIds,
            Is.EqualTo([ "function.target", "function.value" ])
        );
    }

    [Test]
    public void ValidatorRejectsUseBeforeDefinition()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Int,
                0,
                [
                    new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null),
                    new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Int, null),
                ],
                [
                    new IrBasicBlock(
                        0,
                        [ new IrInstruction.Copy(default, 1, 0) ],
                        new IrTerminator.Return(default, 1)
                    ),
                ]
            ),
            [ ]
        );

        DiagnosticCollection diagnostics = IrValidator.Validate(program, environment);

        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.UseBeforeDefinition)
        );
    }

    [Test]
    public void ValidatorReportsMissingBlockTargetsWithoutThrowing()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Void,
                0,
                [ ],
                [
                    new IrBasicBlock(
                        0,
                        [ ],
                        new IrTerminator.Jump(default, 1)
                    ),
                ]
            ),
            [ ]
        );

        DiagnosticCollection diagnostics = IrValidator.Validate(program, environment);
        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidStructure)
        );
    }

    [Test]
    public void ValidatorRejectsDuplicateObjectProperties()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        ObjectTypeSymbol objectType = ObjectTypeSymbol.CreateAnonymous(
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                objectType,
                0,
                [
                    new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null),
                    new IrSlot(1, IrSlotKind.Temporary, objectType, null),
                ],
                [
                    new IrBasicBlock(
                        0,
                        [
                            new IrInstruction.Constant(
                                default,
                                0,
                                TypeSymbols.Int,
                                1L
                            ),
                            new IrInstruction.CreateObject(
                                default,
                                1,
                                objectType,
                                [
                                    new IrInstruction.ObjectPropertyValue("value", 0),
                                    new IrInstruction.ObjectPropertyValue("value", 0),
                                ]
                            ),
                        ],
                        new IrTerminator.Return(default, 1)
                    ),
                ]
            ),
            [ ]
        );

        DiagnosticCollection diagnostics = IrValidator.Validate(program, environment);

        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.InvalidStructure)
        );
    }

    private static Func<DotNetRuntimeContext, object?> CompileExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null
    )
    {
        IrProgram program = LowerExpression(source, environment, expectedType);

        return Export(program, environment);
    }

    private static Func<DotNetRuntimeContext, object?> CompileProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        IrProgram program = LowerProgram(source, environment, resultType);

        return Export(program, environment);
    }

    private static IrProgram LowerExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null
    )
    {
        CompilationResult result = MuLangCompiler.Compile(
            source,
            environment,
            CompilationMode.Expression,
            expectedType,
            GetProfile(environment)
        );
        Assert.That(result.Diagnostics, Is.Empty);

        return result.Program ??
            throw new AssertionException("Expected compilation to produce an IR program.");
    }

    private static IrProgram LowerProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        CompilationResult result = MuLangCompiler.Compile(
            source,
            environment,
            CompilationMode.Program,
            resultType,
            GetProfile(environment)
        );
        Assert.That(result.Diagnostics, Is.Empty);

        return result.Program ??
            throw new AssertionException("Expected compilation to produce an IR program.");
    }

    private static Func<DotNetRuntimeContext, object?> Export(
        IrProgram program,
        EnvironmentSchema environment
    )
    {
        DotNetExportResult result = DotNetExporter.Export(program, environment);
        Assert.That(result.Diagnostics, Is.Empty);

        return result.Delegate ??
            throw new AssertionException("Expected the .NET exporter to produce a delegate.");
    }

    private static EnvironmentSchema CreateEmptyEnvironment(
        LanguageVersion languageVersion = LanguageVersion.Version1
    )
    {
        return new EnvironmentBuilder().Build(languageVersion);
    }

    private static LanguageProfile GetProfile(EnvironmentSchema environment)
    {
        return environment.LanguageVersion == LanguageVersion.Version1_1
            ? LanguageProfiles.Version1_1
            : LanguageProfiles.Version1;
    }

    private static DotNetRuntimeContext CreateContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>>? globals = null,
        IEnumerable<KeyValuePair<string, DotNetFunction>>? functions = null,
        long? executionBudget = null,
        int maximumTraversalDepth = 256,
        int maximumUserFunctionCallDepth = 256,
        CancellationToken cancellationToken = default
    )
    {
        return new DotNetRuntimeContext(
            environment,
            globals ?? [ ],
            functions ?? [ ],
            executionBudget,
            maximumTraversalDepth,
            cancellationToken,
            maximumUserFunctionCallDepth
        );
    }

    private static MuLangRuntimeException RequireRuntimeException(Action action)
    {
        return Assert.Throws<MuLangRuntimeException>(action) ??
            throw new AssertionException("Expected a MuLang runtime exception.");
    }

    private static object CreateNestedObject(int depth)
    {
        object current = new MutableObjectValue(
            [ new KeyValuePair<string, object?>("value", 1L) ]
        );

        for (int index = 0; index < depth; index++)
        {
            current = new MutableObjectValue(
                [ new KeyValuePair<string, object?>("next", current) ]
            );
        }

        return current;
    }

    private sealed class MutableObjectValue : IDotNetObjectValue
    {
        private readonly Dictionary<string, object?> properties;

        public MutableObjectValue(
            IEnumerable<KeyValuePair<string, object?>> properties
        )
        {
            this.properties = properties.ToDictionary(
                static property => property.Key,
                static property => property.Value,
                StringComparer.Ordinal
            );
        }

        public object Identity => this;

        public IReadOnlyCollection<string> PropertyNames => properties.Keys;

        public bool TryGetProperty(string name, out object? value)
        {
            return properties.TryGetValue(name, out value);
        }

        public bool TrySetProperty(string name, object? value)
        {
            properties[name] = value;
            return true;
        }

        public bool TryRemoveProperty(string name)
        {
            return properties.Remove(name);
        }

        public void Set(string name, object? value)
        {
            properties[name] = value;
        }
    }

    private sealed class MutableArrayValue : IDotNetArrayValue
    {
        private readonly IList<object?> elements;

        public MutableArrayValue(IEnumerable<object?> elements)
        {
            this.elements = [ .. elements ];
        }

        public object Identity => this;

        public int Count => elements.Count;

        public bool TryGetElement(int index, out object? value)
        {
            if (index < 0 || index >= elements.Count)
            {
                value = null;
                return false;
            }

            value = elements[index];
            return true;
        }

        public bool TrySetElement(int index, object? value)
        {
            if (index < 0 || index >= elements.Count)
            {
                return false;
            }

            elements[index] = value;
            return true;
        }
    }

    private sealed class ReadOnlyArrayValue : IDotNetReadOnlyArrayValue
    {
        private readonly IReadOnlyList<object?> elements;

        public ReadOnlyArrayValue(IEnumerable<object?> elements)
        {
            this.elements = [ .. elements ];
        }

        public object Identity => this;

        public int Count => elements.Count;

        public bool TryGetElement(int index, out object? value)
        {
            if (index < 0 || index >= elements.Count)
            {
                value = null;
                return false;
            }

            value = elements[index];
            return true;
        }
    }

    private sealed class ReadOnlyObjectValue : IDotNetObjectValue
    {
        private readonly string name;
        private readonly object? value;

        public ReadOnlyObjectValue(string name, object? value)
        {
            this.name = name;
            this.value = value;
        }

        public object Identity => this;

        public IReadOnlyCollection<string> PropertyNames => [ name ];

        public bool TryGetProperty(string propertyName, out object? propertyValue)
        {
            if (propertyName == name)
            {
                propertyValue = value;
                return true;
            }

            propertyValue = null;
            return false;
        }

        public bool TrySetProperty(string propertyName, object? propertyValue)
        {
            return false;
        }

        public bool TryRemoveProperty(string propertyName)
        {
            return false;
        }
    }
}
