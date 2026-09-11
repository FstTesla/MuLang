using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;
using MuLang.IR;

namespace MuLang.Tests.Exporting;

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
                    static (_, arguments) =>
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
                    (_, _) =>
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
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Nullable(TypeSymbols.Array(TypeSymbols.Int))
            )
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values?.[1 / 0]",
            environment,
            TypeSymbols.Nullable(TypeSymbols.Int)
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", null) ]
        );

        Assert.That(compiled(context), Is.Null);
    }

    [Test]
    public void WrapsMinimumIntegerRemainderOverflow()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "-9223372036854775808 % -1",
            environment
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(CreateContext(environment))
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
    public void CheckedNumberCastsUseTheRuntimeNumericKind()
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
            [new KeyValuePair<string, object?>("global.value", 2L)]
        );
        DotNetRuntimeContext floatContext = CreateContext(
            environment,
            [new KeyValuePair<string, object?>("global.value", 2.0)]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(toInt(integerContext), Is.EqualTo(2L));
            Assert.That(toFloat(integerContext), Is.EqualTo(2.0));
            Assert.That(toInt(floatContext), Is.EqualTo(2L));
        }
    }

    [Test]
    public void TypeTestsUseNumericAssignability()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> intIsFloat = CompileExpression(
            "1 is float",
            environment
        );
        Func<DotNetRuntimeContext, object?> floatIsInt = CompileExpression(
            "1.0 is int",
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(intIsFloat(CreateContext(environment)), Is.True);
            Assert.That(floatIsInt(CreateContext(environment)), Is.False);
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
            [new KeyValuePair<string, object?>("global.value", 1L)]
        );
        DotNetRuntimeContext integerNumberContext = CreateContext(
            numberEnvironment,
            [new KeyValuePair<string, object?>("global.value", 1L)]
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
    public void UnknownCannotBypassFloatToIntConversionRule()
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
            [new KeyValuePair<string, object?>("global.value", 2.0)]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
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
    public void FormatsNumberConversionAsNumberLiteral()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "1.0 as string",
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo("1.0"));
    }

    [Test]
    public void TreatsClrArraysAsZeroBasedLanguageArrays()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.Array(TypeSymbols.Int)
            )
            .Build();
        Array values = Array.CreateInstance(typeof(long), [ 1 ], [ 1 ]);
        values.SetValue(42L, 1);
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "values[0]",
            environment
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.values", values) ]
        );

        Assert.That(compiled(context), Is.EqualTo(42L));
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
                    static (_, _) => "invalid"
                ),
            ]
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6015"));
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
                    (_, _) =>
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
        Dictionary<string, object?> item = new (StringComparer.Ordinal)
        {
            ["id"] = 42L,
        };
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
        Dictionary<string, object?> left = new (StringComparer.Ordinal);
        Dictionary<string, object?> right = new (StringComparer.Ordinal);
        left["self"] = left;
        right["self"] = right;
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
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Bool)
            .Build();
        IrProgram program = LowerExpression("true || touch()", environment);

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
        string[] callIds =
        [
            .. program.Blocks
                .SelectMany(static block => block.Instructions)
                .OfType<IrInstruction.Call>()
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
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Expression
        );
        BindingResult binding = Binder.Bind(syntaxTree, environment, expectedType);

        return Lower(binding, environment);
    }

    private static IrProgram LowerProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Program
        );
        BindingResult binding = Binder.Bind(syntaxTree, environment, resultType);

        return Lower(binding, environment);
    }

    private static IrProgram Lower(
        BindingResult binding,
        EnvironmentSchema environment
    )
    {
        Assert.That(binding.Diagnostics, Is.Empty);
        LoweringResult lowering = Lowerer.Lower(binding, environment);
        Assert.That(lowering.Diagnostics, Is.Empty);

        return lowering.Program ??
            throw new AssertionException("Expected lowering to produce an IR program.");
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

    private static EnvironmentSchema CreateEmptyEnvironment()
    {
        return new EnvironmentBuilder().Build();
    }

    private static DotNetRuntimeContext CreateContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>>? globals = null,
        IEnumerable<KeyValuePair<string, DotNetFunction>>? functions = null,
        long executionBudget = long.MaxValue,
        CancellationToken cancellationToken = default
    )
    {
        return new DotNetRuntimeContext(
            environment,
            globals ?? [ ],
            functions ?? [ ],
            executionBudget,
            cancellationToken
        );
    }

    private static MuLangRuntimeException RequireRuntimeException(Action action)
    {
        return Assert.Throws<MuLangRuntimeException>(action) ??
            throw new AssertionException("Expected a MuLang runtime exception.");
    }

    private static object CreateNestedObject(int depth)
    {
        object current = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = 1L,
        };

        for (int index = 0; index < depth; index++)
        {
            current = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["next"] = current,
            };
        }

        return current;
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
