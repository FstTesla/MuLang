using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
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

namespace MuLang.Tests.Functions;

public sealed class UserFunctionTests
{
    [Test]
    public void ParsesFunctionDeclaration()
    {
        SyntaxTree tree = ParseProgram(
            "func add(left: int, right: int): int { return left + right; }"
        );
        ProgramRootSyntax root = (ProgramRootSyntax)tree.Root;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Functions, Has.Count.EqualTo(1));
            Assert.That(root.Functions[0].Parameters, Has.Count.EqualTo(2));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void RejectsFunctionsInExpressionMode()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("func value(): int { return 1; }"),
            CompilationMode.Expression
        );

        AssertDiagnostic(tree.Diagnostics, DiagnosticCodes.FunctionDeclarationNotAllowed);
    }

    [Test]
    public void ReportsFunctionAfterExecutableStatement()
    {
        SyntaxTree tree = ParseProgram(
            "var value = 1; func get(): int { return value; }"
        );

        AssertDiagnostic(tree.Diagnostics, DiagnosticCodes.FunctionDeclarationAfterStatement);
    }

    [Test]
    public void RecoversLaterTopLevelFunctionAfterMalformedDeclaration()
    {
        const string source = """
                              func broken(a: int b: int): int { return 0; }
                              func good(): int { return 1; }
                              return good();
                              """;
        SyntaxTree tree = ParseProgram(source);
        ProgramRootSyntax root = (ProgramRootSyntax)tree.Root;

        Assert.That(
            root.Functions.Select(
                static function =>
                    function.IdentifierToken
            ),
            Has.Some.Matches<SyntaxToken>(
                token => tree.Source.GetText(token.Span) == "good"
            )
        );
    }

    [Test]
    public void BindsForwardCallsAndMutualRecursion()
    {
        const string source = """
                              func even(value: int): bool {
                                  if (value == 0)
                                      return true;
                                  return odd(value - 1);
                              }
                              func odd(value: int): bool {
                                  if (value == 0)
                                      return false;
                                  return even(value - 1);
                              }
                              return even(6);
                              """;
        BindingResult result = BindProgram(source, CreateEmptyEnvironment(), TypeSymbols.Bool);

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ExecutesForwardCallsAndMutualRecursion()
    {
        const string source = """
                              func even(value: int): bool {
                                  if (value == 0)
                                      return true;
                                  return odd(value - 1);
                              }
                              func odd(value: int): bool {
                                  if (value == 0)
                                      return false;
                                  return even(value - 1);
                              }
                              return even(7);
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
    public void ExecutesDirectRecursion()
    {
        const string source = """
                              func factorial(value: int): int {
                                  if (value <= 1)
                                      return 1;
                                  return value * factorial(value - 1);
                              }
                              return factorial(5);
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );

        Assert.That(compiled(CreateContext(environment)), Is.EqualTo(120L));
    }

    [Test]
    public void ExecutesVoidFunctionAndProviderCall()
    {
        const string source = """
                              func write(value: int): void {
                                  log(value);
                              }
                              write(42);
                              """;
        long observed = 0;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction(
                "function.log",
                "log",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.Void
            )
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Void
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.log",
                    arguments =>
                    {
                        observed = (long)(arguments[0] ??
                            throw new AssertionException("Expected an argument."));
                        return null;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.Null);
            Assert.That(observed, Is.EqualTo(42L));
        }
    }

    [Test]
    public void ValidatesDeclarationAndParameterRules()
    {
        const string source = """
                              func duplicate(value: int, value: int): int { return value; }
                              func duplicate(): int { return 1; }
                              """;
        BindingResult result = BindProgram(source, CreateEmptyEnvironment(), TypeSymbols.Void);

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.DuplicateParameter);
        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.DuplicateFunction);
    }

    [Test]
    public void RejectsVoidParameterType()
    {
        BindingResult result = BindProgram(
            "func invalid(value: void): void { }",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.InvalidParameterType);
    }

    [TestCase("func invalid(): void? { }")]
    [TestCase("func invalid(value: void[]): void { }")]
    public void RejectsQualifiedVoidTypesWithoutThrowing(string source)
    {
        Assert.DoesNotThrow(
            () =>
            {
                BindingResult result = BindProgram(
                    source,
                    CreateEmptyEnvironment(),
                    TypeSymbols.Void
                );
                Assert.That(result.Diagnostics.HasErrors, Is.True);
            }
        );
    }

    [Test]
    public void RejectsProviderFunctionConflicts()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.value", "value", [ ], TypeSymbols.Int)
            .Build();
        BindingResult result = BindProgram(
            "func value(): int { return 1; }",
            environment,
            TypeSymbols.Void
        );

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.FunctionConflict);
    }

    [Test]
    public void RejectsParameterAssignmentAndShadowing()
    {
        const string source = """
                              func invalid(value: int): int {
                                  value = 2;
                                  var value = 3;
                                  return value;
                              }
                              """;
        BindingResult result = BindProgram(source, CreateEmptyEnvironment(), TypeSymbols.Void);

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.CannotAssignParameter);
        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.ShadowedVariable);
    }

    [Test]
    public void ValidatesFunctionReturnPaths()
    {
        BindingResult result = BindProgram(
            "func value(flag: bool): int { if (flag) return 1; }",
            CreateEmptyEnvironment(),
            TypeSymbols.Void
        );

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.NotAllPathsReturn);
    }

    [Test]
    public void FunctionCannotAccessTopLevelLocals()
    {
        const string source = """
                              func read(): int { return value; }
                              var value = 1;
                              return read();
                              """;
        BindingResult result = BindProgram(source, CreateEmptyEnvironment(), TypeSymbols.Int);

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.UndefinedName);
    }

    [Test]
    public void LoopControlCannotCrossFunctionBoundary()
    {
        const string source = """
                              func invalid(): void {
                                  while (true)
                                      break 2;
                              }
                              """;
        BindingResult result = BindProgram(source, CreateEmptyEnvironment(), TypeSymbols.Void);

        AssertDiagnostic(result.Diagnostics, DiagnosticCodes.InvalidLoopLevel);
    }

    [Test]
    public void UserCallArgumentsEvaluateLeftToRight()
    {
        const string source = """
                              func combine(left: int, right: int): int {
                                  return left * 10 + right;
                              }
                              return combine(first(), second());
                              """;
        List<string> calls = [ ];
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.first", "first", [ ], TypeSymbols.Int)
            .AddFunction("function.second", "second", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.first",
                    _ =>
                    {
                        calls.Add("first");
                        return 1L;
                    }
                ),
                new KeyValuePair<string, DotNetFunction>(
                    "function.second",
                    _ =>
                    {
                        calls.Add("second");
                        return 2L;
                    }
                ),
            ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.EqualTo(12L));
            Assert.That(calls, Is.EqualTo([ "first", "second" ]));
        }
    }

    [Test]
    public void LowersIndependentFunctionFramesAndUserCalls()
    {
        const string source = """
                              func add(value: int): int { return value + 1; }
                              return add(2);
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram program = LowerProgram(source, environment, TypeSymbols.Int);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(program.UserFunctions, Has.Count.EqualTo(1));
            Assert.That(
                program.EntryFunction.Blocks
                    .SelectMany(static block => block.Instructions)
                    .OfType<IrInstruction.UserCall>(),
                Has.Exactly(1).Items
            );
            Assert.That(
                program.UserFunctions[0].Slots.Count(
                    static slot => slot.Kind == IrSlotKind.Parameter
                ),
                Is.EqualTo(1)
            );
            Assert.That(IrValidator.Validate(program, environment), Is.Empty);
        }
    }

    [Test]
    public void EnforcesMaximumUserFunctionCallDepth()
    {
        const string source = """
                              func recurse(): int { return recurse(); }
                              return recurse();
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            maximumUserFunctionCallDepth: 4
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6017"));
    }

    [Test]
    public void ChargesUserFunctionCallsToExecutionBudget()
    {
        const string source = """
                              func recurse(): int { return recurse(); }
                              return recurse();
                              """;
        EnvironmentSchema environment = CreateEmptyEnvironment();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = CreateContext(
            environment,
            executionBudget: 8,
            maximumUserFunctionCallDepth: 100
        );

        MuLangRuntimeException exception = RequireRuntimeException(
            () => compiled(context)
        );

        Assert.That(exception.Code, Is.EqualTo("MUL6011"));
    }

    [Test]
    public void CallDepthIsIndependentForReentrantExecutions()
    {
        const string source = """
                              func value(): int { return reenter(); }
                              return value();
                              """;
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction("function.reenter", "reenter", [ ], TypeSymbols.Int)
            .Build();
        Func<DotNetRuntimeContext, object?> compiled = CompileProgram(
            source,
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext? context = null;
        int invocationCount = 0;
        context = CreateContext(
            environment,
            functions:
            [
                new KeyValuePair<string, DotNetFunction>(
                    "function.reenter",
                    _ =>
                    {
                        invocationCount++;

                        return invocationCount == 1
                            ? compiled(
                                // ReSharper disable once AccessToModifiedClosure
                                context ??
                                throw new AssertionException("Expected a context.")
                            )
                            : 42L;
                    }
                ),
            ],
            maximumUserFunctionCallDepth: 1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiled(context), Is.EqualTo(42L));
            Assert.That(invocationCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void ValidatorRejectsEntryFunctionParameters()
    {
        EnvironmentSchema environment = CreateEmptyEnvironment();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Program,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Int,
                0,
                [ new IrSlot(0, IrSlotKind.Parameter, TypeSymbols.Int, "value") ],
                [
                    new IrBasicBlock(
                        0,
                        [ ],
                        new IrTerminator.Return(default, 0)
                    ),
                ]
            ),
            [ ]
        );

        DiagnosticCollection diagnostics = IrValidator.Validate(program, environment);

        AssertDiagnostic(diagnostics, IrDiagnosticCodes.InvalidSlot);
    }

    private static SyntaxTree ParseProgram(string source)
    {
        return Parser.Parse(SourceText.From(source), CompilationMode.Program);
    }

    private static BindingResult BindProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        return Binder.Bind(ParseProgram(source), environment, resultType);
    }

    private static IrProgram LowerProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        BindingResult binding = BindProgram(source, environment, resultType);
        Assert.That(binding.Diagnostics, Is.Empty);
        LoweringResult lowering = Lowerer.Lower(binding, environment);
        Assert.That(lowering.Diagnostics, Is.Empty);

        return lowering.Program ??
            throw new AssertionException("Expected an IR program.");
    }

    private static Func<DotNetRuntimeContext, object?> CompileProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        IrProgram program = LowerProgram(source, environment, resultType);
        DotNetExportResult result = DotNetExporter.Export(program, environment);
        Assert.That(result.Diagnostics, Is.Empty);

        return result.Delegate ??
            throw new AssertionException("Expected a compiled delegate.");
    }

    private static EnvironmentSchema CreateEmptyEnvironment()
    {
        return new EnvironmentBuilder().Build();
    }

    private static DotNetRuntimeContext CreateContext(
        EnvironmentSchema environment,
        IEnumerable<KeyValuePair<string, object?>>? globals = null,
        IEnumerable<KeyValuePair<string, DotNetFunction>>? functions = null,
        long? executionBudget = null,
        int maximumTraversalDepth = 256,
        int maximumUserFunctionCallDepth = 256
    )
    {
        return new DotNetRuntimeContext(
            environment,
            globals ?? [ ],
            functions ?? [ ],
            executionBudget,
            maximumTraversalDepth,
            CancellationToken.None,
            maximumUserFunctionCallDepth
        );
    }

    private static MuLangRuntimeException RequireRuntimeException(Action action)
    {
        return Assert.Throws<MuLangRuntimeException>(action) ??
            throw new AssertionException("Expected a MuLang runtime exception.");
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
}
