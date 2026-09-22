using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;
using MuLang.IR;
using MuLang.TestSupport;

namespace MuLang.Compiler.Tests.Language;

public sealed class TruthinessTests
{
    private static readonly LanguageProfile truthinessProfile =
        TestLanguageProfileFactory.Create(
            conditionSemantics: ConditionSemantics.Truthiness
        );

    [Test]
    public void CompilesThroughThePublicProfileAwarePipeline()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        CompilationResult result = MuLangCompiler.Compile(
            "0 || \"value\"",
            environment,
            CompilationMode.Expression,
            TypeSymbols.Bool,
            truthinessProfile
        );
        DotNetExportResult export = DotNetExporter.Export(
            result.Program ?? throw new AssertionException("Expected compilation to produce an IR program."),
            environment,
            CompilationMode.Expression,
            truthinessProfile.Fingerprint
        );
        Func<DotNetRuntimeContext, object?> compiled = export.Delegate ??
            throw new AssertionException("Expected the .NET exporter to produce a delegate.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(export.Diagnostics, Is.Empty);
            Assert.That(compiled(CreateContext(environment)), Is.True);
        }
    }

    [TestCase("if (1) return true; return false;", true)]
    [TestCase("if (0) return true; return false;", false)]
    [TestCase("while (\"value\") return true;", true)]
    [TestCase("while (\"\") return true; return false;", false)]
    [TestCase("for (; -1.0; ) return true;", true)]
    [TestCase("for (; 0.0; ) return true; return false;", false)]
    public void ExecutesTruthyStatementConditions(string source, bool expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(expected));
    }

    [TestCase("\"value\" ? true : false", true)]
    [TestCase("\"\" ? true : false", false)]
    public void ExecutesTruthyConditionalExpression(string source, bool expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(expected));
    }

    [TestCase("!0", true)]
    [TestCase("1 && \"\"", false)]
    [TestCase("0 || \"value\"", true)]
    public void ExecutesTruthyLogicalOperators(string source, bool expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        BindingResult binding = BindExpression(
            source,
            environment,
            TypeSymbols.Bool
        );
        BoundRoot.Expression root = (BoundRoot.Expression)binding.Root;
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment,
            TypeSymbols.Bool
        );
        object? result = compiled(CreateContext(environment));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(binding.Diagnostics, Is.Empty);
            Assert.That(root.Value.Type, Is.SameAs(TypeSymbols.Bool));
            Assert.That(result, Is.TypeOf<bool>());
            Assert.That(result, Is.EqualTo(expected));
        }
    }

    [TestCase("!!null", false)]
    [TestCase("null || true", true)]
    public void LowersDirectNullTruthiness(string source, bool expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(TruthinessValueCases))]
    public void AppliesSettledRuntimeTruthiness(
        TypeSymbol type,
        object? value,
        bool expected
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", type)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "!!value",
            environment,
            TypeSymbols.Bool
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", value) ]
        );

        Assert.That(compiled(context), Is.EqualTo(expected));
    }

    [Test]
    public void DispatchesNumberTruthinessByRuntimeKind()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "!!value",
            environment,
            TypeSymbols.Bool
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                compiled(
                    CreateContext(
                        environment,
                        [ new KeyValuePair<string, object?>("global.value", 0L) ]
                    )
                ),
                Is.False
            );
            Assert.That(
                compiled(
                    CreateContext(
                        environment,
                        [ new KeyValuePair<string, object?>("global.value", -2.5) ]
                    )
                ),
                Is.True
            );
        }
    }

    [Test]
    public void AcceptsUnknownAndNullableUnknownConditions()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.dynamic", "dynamic", TypeSymbols.Unknown)
            .AddGlobal(
                "global.optional",
                "optional",
                TypeSymbols.Nullable(TypeSymbols.Unknown)
            )
            .Build();
        Func<DotNetRuntimeContext, object?> dynamicCondition = CompileExpression(
            "!!dynamic",
            environment,
            TypeSymbols.Bool
        );
        Func<DotNetRuntimeContext, object?> optionalCondition = CompileExpression(
            "!!optional",
            environment,
            TypeSymbols.Bool
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>("global.dynamic", "value"),
                new KeyValuePair<string, object?>("global.optional", null),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dynamicCondition(context), Is.True);
            Assert.That(optionalCondition(context), Is.False);
        }
    }

    [TestCase("0 && touch()", false)]
    [TestCase("\"value\" || touch()", true)]
    public void PreservesTruthyShortCircuiting(string source, bool expected)
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment,
            TypeSymbols.Bool
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
                        return 1L;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.EqualTo(expected));
            Assert.That(invocationCount, Is.Zero);
        }
    }

    [TestCase("1 & 3", 1L)]
    [TestCase("1 | 2", 3L)]
    [TestCase("1 ^ 3", 2L)]
    [TestCase("true & false", false)]
    [TestCase("true | false", true)]
    [TestCase("true ^ false", true)]
    public void LeavesEagerOperatorsUnchanged(string source, object expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(expected));
    }

    [TestCase("true & 1")]
    [TestCase("1 | false")]
    [TestCase("\"value\" ^ true")]
    public void KeepsEagerOperatorsHomogeneous(string source)
    {
        BindingResult result = BindExpression(
            source,
            CreateEmptyEnvironment()
        );

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.OperatorNotDefined);
    }

    [Test]
    public void DoesNotIntroduceGeneralBooleanConversions()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        BindingResult assignment = BindProgram(
            "var value: bool = 1;",
            environment,
            TypeSymbols.Void
        );
        BindingResult cast = BindExpression(
            "1 as bool",
            environment
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(assignment.Diagnostics, DiagnosticCodes.TypeMismatch);
            AssertDiagnostic(cast.Diagnostics, DiagnosticCodes.InvalidConversion);
        }
    }

    [TestCase("0 == false", false)]
    [TestCase("0 != false", true)]
    [TestCase("0 === false", false)]
    [TestCase("0 !== false", true)]
    public void DoesNotChangeEqualitySemantics(string source, bool expected)
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            source,
            environment,
            TypeSymbols.Bool
        );

        Assert.That(
            compiled(CreateContext(environment)),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void DoesNotAccessAdaptersForTruthiness()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.object", "objectValue", TypeSymbols.Unknown)
            .AddGlobal("global.array", "arrayValue", TypeSymbols.Unknown)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "objectValue && arrayValue",
            environment,
            TypeSymbols.Bool
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [
                new KeyValuePair<string, object?>(
                    "global.object",
                    new InaccessibleObjectValue()
                ),
                new KeyValuePair<string, object?>(
                    "global.array",
                    new InaccessibleArrayValue()
                ),
            ]
        );

        Assert.That(compiled(context), Is.True);
    }

    [Test]
    public void EvaluatesCollectionLiteralContentsBeforeTruthiness()
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "!![touch()]",
            environment,
            TypeSymbols.Bool
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
                        return 1L;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.True);
            Assert.That(invocationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void EvaluatesConstantTruthyForCondition()
    {
        int invocationCount = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            "for (; [touch()]; ) break;",
            environment,
            TypeSymbols.Void
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
                        return 1L;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.Null);
            Assert.That(invocationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RejectsUnsupportedRuntimeRepresentations()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Unknown)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileExpression(
            "!!value",
            environment,
            TypeSymbols.Bool
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            [ new KeyValuePair<string, object?>("global.value", new object()) ]
        );
        MuLangRuntimeException exception =
            Assert.Throws<MuLangRuntimeException>(() => compiled(context)) ??
            throw new AssertionException("Expected a MuLang runtime exception.");

        Assert.That(
            exception.Code,
            Is.EqualTo(DotNetRuntimeErrorCodes.InvalidRuntimeValue)
        );
    }

    [Test]
    public void NormalizesAllConditionContextsInTheBoundTree()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        BindingResult programBinding = BindProgram(
            """
            if (value) { }
            while (value) break;
            for (; value; ) break;
            """,
            environment,
            TypeSymbols.Void
        );
        BoundRoot.Program program = (BoundRoot.Program)programBinding.Root;
        BoundStatement.If conditional = (BoundStatement.If)program.Statements[0];
        BoundStatement.While whileLoop = (BoundStatement.While)program.Statements[1];
        BoundStatement.For forLoop = (BoundStatement.For)program.Statements[2];
        BindingResult expressionBinding = BindExpression(
            "value ? true : false",
            environment,
            TypeSymbols.Bool
        );
        BoundExpression.Conditional expression =
            (BoundExpression.Conditional)
            ((BoundRoot.Expression)expressionBinding.Root).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(programBinding.Diagnostics, Is.Empty);
            Assert.That(expressionBinding.Diagnostics, Is.Empty);
            Assert.That(
                conditional.Condition,
                Is.TypeOf<BoundExpression.Truthiness>()
            );
            Assert.That(
                whileLoop.Condition,
                Is.TypeOf<BoundExpression.Truthiness>()
            );
            Assert.That(
                forLoop.Condition,
                Is.TypeOf<BoundExpression.Truthiness>()
            );
            Assert.That(
                expression.Condition,
                Is.TypeOf<BoundExpression.Truthiness>()
            );
        }
    }

    [Test]
    public void NormalizesLogicalOperandsInTheBoundTree()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.left", "left", TypeSymbols.Unknown)
            .AddGlobal(
                "global.right",
                "right",
                TypeSymbols.Nullable(TypeSymbols.String)
            )
            .Build();
        BindingResult binaryBinding = BindExpression(
            "left && right",
            environment,
            TypeSymbols.Bool
        );
        BoundExpression.Binary binary =
            (BoundExpression.Binary)
            ((BoundRoot.Expression)binaryBinding.Root).Value;
        BindingResult unaryBinding = BindExpression(
            "!left",
            environment,
            TypeSymbols.Bool
        );
        BoundExpression.Unary unary =
            (BoundExpression.Unary)
            ((BoundRoot.Expression)unaryBinding.Root).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(binaryBinding.Diagnostics, Is.Empty);
            Assert.That(unaryBinding.Diagnostics, Is.Empty);
            Assert.That(binary.Left, Is.TypeOf<BoundExpression.Truthiness>());
            Assert.That(binary.Right, Is.TypeOf<BoundExpression.Truthiness>());
            Assert.That(unary.Operand, Is.TypeOf<BoundExpression.Truthiness>());
        }
    }

    [Test]
    public void RejectsVoidTruthyContexts()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.touch", "touch", [ ], TypeSymbols.Void)
            .Build();
        BindingResult condition = BindProgram(
            "if (touch()) { }",
            environment,
            TypeSymbols.Void
        );
        BindingResult unary = BindExpression("!touch()", environment);
        BindingResult binary = BindExpression("touch() && true", environment);

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(condition.Diagnostics, DiagnosticCodes.TypeMismatch);
            AssertDiagnostic(unary.Diagnostics, DiagnosticCodes.OperatorNotDefined);
            AssertDiagnostic(binary.Diagnostics, DiagnosticCodes.OperatorNotDefined);
        }
    }

    [Test]
    public void StrictBooleanProfilePreservesConditionDiagnostics()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        BindingResult condition = BindProgram(
            "if (1) { }",
            environment,
            TypeSymbols.Void,
            LanguageProfiles.Version1
        );
        BindingResult unary = BindExpression(
            "!1",
            environment,
            profile: LanguageProfiles.Version1
        );
        BindingResult binary = BindExpression(
            "1 && true",
            environment,
            profile: LanguageProfiles.Version1
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(condition.Diagnostics, DiagnosticCodes.TypeMismatch);
            AssertDiagnostic(unary.Diagnostics, DiagnosticCodes.OperatorNotDefined);
            AssertDiagnostic(binary.Diagnostics, DiagnosticCodes.OperatorNotDefined);
        }
    }

    [Test]
    public void RecognizesConstantTruthyLoopsAsNonCompleting()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        CompilationResult result = MuLangCompiler.Compile(
            "while (1) { }",
            environment,
            CompilationMode.Program,
            TypeSymbols.Int,
            truthinessProfile
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.NotAllPathsReturn)
            );
            Assert.That(result.IsSuccessful, Is.True);
        }
    }

    [Test]
    public void LowersTruthinessToDedicatedIrInstruction()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        IrProgram program = LowerExpression(
            "!!value",
            environment,
            TypeSymbols.Bool
        );
        IReadOnlyList<IrInstruction.Truthiness> instructions =
            [ .. program.Blocks.SelectMany(static block => block.Instructions).OfType<IrInstruction.Truthiness>() ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instructions, Has.Count.EqualTo(1));
            Assert.That(
                program.Slots[instructions[0].Destination].Type,
                Is.SameAs(TypeSymbols.Bool)
            );
            Assert.That(
                program.Slots[instructions[0].Source].Type,
                Is.SameAs(TypeSymbols.Int)
            );
        }
    }

    [Test]
    public void DoesNotLowerBooleanValuesThroughTruthiness()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram program = LowerExpression(
            "!true",
            environment,
            TypeSymbols.Bool
        );

        Assert.That(
            program.Blocks
                .SelectMany(static block => block.Instructions)
                .OfType<IrInstruction.Truthiness>(),
            Is.Empty
        );
    }

    [Test]
    public void ValidatorChecksTruthinessSlotsAndDefinitions()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram missingSlots = CreateIrProgram(
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Bool, null),
            ],
            [ new IrInstruction.Truthiness(default, 2, 1) ],
            new IrTerminator.Return(default, null)
        );
        IrProgram invalidTypes = CreateIrProgram(
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Void, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Int, null),
            ],
            [ new IrInstruction.Truthiness(default, 1, 0) ],
            new IrTerminator.Return(default, null)
        );
        IrProgram undefinedSource = CreateIrProgram(
            [
                new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null),
                new IrSlot(1, IrSlotKind.Temporary, TypeSymbols.Bool, null),
            ],
            [ new IrInstruction.Truthiness(default, 1, 0) ],
            new IrTerminator.Return(default, 1)
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(
                IrValidator.Validate(missingSlots, environment),
                IrDiagnosticCodes.InvalidSlot
            );
            AssertDiagnostic(
                IrValidator.Validate(invalidTypes, environment),
                IrDiagnosticCodes.TypeMismatch
            );
            AssertDiagnostic(
                IrValidator.Validate(undefinedSource, environment),
                IrDiagnosticCodes.UseBeforeDefinition
            );
        }
    }

    private static IEnumerable<TestCaseData> TruthinessValueCases()
    {
        yield return new TestCaseData(
            TypeSymbols.Nullable(TypeSymbols.Unknown),
            null,
            false
        ).SetName("NullIsFalsy");
        yield return new TestCaseData(TypeSymbols.Bool, false, false)
            .SetName("FalseIsFalsy");
        yield return new TestCaseData(TypeSymbols.Int, 0L, false)
            .SetName("IntegerZeroIsFalsy");
        yield return new TestCaseData(TypeSymbols.Float, 0.0, false)
            .SetName("PositiveFloatZeroIsFalsy");
        yield return new TestCaseData(TypeSymbols.Float, -0.0, false)
            .SetName("NegativeFloatZeroIsFalsy");
        yield return new TestCaseData(TypeSymbols.Float, double.NaN, false)
            .SetName("NaNIsFalsy");
        yield return new TestCaseData(TypeSymbols.String, "", false)
            .SetName("EmptyStringIsFalsy");
        yield return new TestCaseData(TypeSymbols.Bool, true, true)
            .SetName("TrueIsTruthy");
        yield return new TestCaseData(TypeSymbols.Int, 1L, true)
            .SetName("PositiveIntegerIsTruthy");
        yield return new TestCaseData(TypeSymbols.Int, -1L, true)
            .SetName("NegativeIntegerIsTruthy");
        yield return new TestCaseData(TypeSymbols.Float, 1.5, true)
            .SetName("PositiveFloatIsTruthy");
        yield return new TestCaseData(TypeSymbols.Float, -1.5, true)
            .SetName("NegativeFloatIsTruthy");
        yield return new TestCaseData(
            TypeSymbols.Float,
            double.PositiveInfinity,
            true
        ).SetName("InfinityIsTruthy");
        yield return new TestCaseData(TypeSymbols.String, "value", true)
            .SetName("NonemptyStringIsTruthy");
        yield return new TestCaseData(
            TypeSymbols.Unknown,
            new TestObjectValue([ ]),
            true
        ).SetName("EmptyObjectIsTruthy");
        yield return new TestCaseData(
            TypeSymbols.Unknown,
            new TestObjectValue(
                [ new KeyValuePair<string, object?>("value", 1L) ]
            ),
            true
        ).SetName("NonemptyObjectIsTruthy");
        yield return new TestCaseData(
            TypeSymbols.Unknown,
            new TestArrayValue([ ]),
            true
        ).SetName("EmptyArrayIsTruthy");
        yield return new TestCaseData(
            TypeSymbols.Unknown,
            new TestArrayValue([ 1L ]),
            true
        ).SetName("NonemptyArrayIsTruthy");
    }

    private static BindingResult BindExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null,
        LanguageProfile? profile = null
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Expression,
            profile ?? truthinessProfile
        );

        return Binder.Bind(syntaxTree, environment, expectedType);
    }

    private static BindingResult BindProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType,
        LanguageProfile? profile = null
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Program,
            profile ?? truthinessProfile
        );

        return Binder.Bind(syntaxTree, environment, resultType);
    }

    private static Func<DotNetRuntimeContext, object?> CompileExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null
    )
    {
        return Export(
            LowerExpression(source, environment, expectedType),
            environment
        );
    }

    private static Func<DotNetRuntimeContext, object?> CompileProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        BindingResult binding = BindProgram(source, environment, resultType);

        return Export(Lower(binding, environment), environment);
    }

    private static IrProgram LowerExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null,
        LanguageProfile? profile = null
    )
    {
        BindingResult binding = BindExpression(
            source,
            environment,
            expectedType,
            profile
        );

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

    private static IrProgram CreateIrProgram(
        IReadOnlyList<IrSlot> slots,
        IReadOnlyCollection<IrInstruction> instructions,
        IrTerminator terminator
    )
    {
        IrFunction entry = new (
            "$entry",
            TypeSymbols.Void,
            0,
            slots,
            [ new IrBasicBlock(0, instructions, terminator) ]
        );

        return new IrProgram(
            CreateEmptyEnvironment().Fingerprint,
            CompilationMode.Program,
            truthinessProfile.Fingerprint,
            entry,
            [ ]
        );
    }

    private static EnvironmentSchema CreateEmptyEnvironment()
    {
        return new EnvironmentBuilder().Build();
    }

    private static DotNetRuntimeContext CreateContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>>? globals = null,
        IEnumerable<KeyValuePair<string, DotNetFunction>>? functions = null
    )
    {
        return new DotNetRuntimeContext(
            environment,
            globals ?? [ ],
            functions ?? [ ]
        );
    }

    private static void AssertDiagnostic(
        DiagnosticCollection diagnostics,
        string code
    )
    {
        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(code)
        );
    }

    private sealed class InaccessibleObjectValue : IDotNetObjectValue
    {
        public object Identity =>
            throw new AssertionException("Truthiness accessed object identity.");

        public IReadOnlyCollection<string> PropertyNames =>
            throw new AssertionException("Truthiness enumerated object properties.");

        public bool TryGetProperty(string name, out object? value)
        {
            throw new AssertionException("Truthiness read an object property.");
        }

        public bool TrySetProperty(string name, object? value)
        {
            throw new AssertionException("Truthiness wrote an object property.");
        }

        public bool TryRemoveProperty(string name)
        {
            throw new AssertionException("Truthiness removed an object property.");
        }
    }

    private sealed class InaccessibleArrayValue : IDotNetArrayValue
    {
        public object Identity =>
            throw new AssertionException("Truthiness accessed array identity.");

        public int Count =>
            throw new AssertionException("Truthiness accessed array count.");

        public bool TryGetElement(int index, out object? value)
        {
            throw new AssertionException("Truthiness read an array element.");
        }

        public bool TrySetElement(int index, object? value)
        {
            throw new AssertionException("Truthiness wrote an array element.");
        }
    }

    private sealed class TestObjectValue : IDotNetObjectValue
    {
        private readonly Dictionary<string, object?> properties;

        public TestObjectValue(
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
    }

    private sealed class TestArrayValue : IDotNetArrayValue
    {
        private readonly IList<object?> elements;

        public TestArrayValue(IEnumerable<object?> elements)
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
}
