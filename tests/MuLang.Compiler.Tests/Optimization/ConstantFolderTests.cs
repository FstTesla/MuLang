using MuLang.Compiler.Diagnostics;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Tests.Optimization;

public sealed class ConstantFolderTests
{
    [Test]
    public void FoldsPrimitiveExpressionToSingleConstant()
    {
        CompilationResult result = CompileExpression("1 + 2 * 3", TypeSymbols.Int);
        IrProgram program = RequireProgram(result);
        IReadOnlyList<IrInstruction> instructions = GetInstructions(program);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(instructions, Has.Count.EqualTo(1));
            Assert.That(
                instructions[0],
                Is.EqualTo(
                    new IrInstruction.Constant(
                        instructions[0].Span,
                        0,
                        TypeSymbols.Int,
                        7L
                    )
                )
            );
        }
    }

    [TestCase("\"value: \" + 1", "value: 1")]
    [TestCase("(1 as unknown) is int", true)]
    [TestCase("1 < 2 && 3 == 3.0", true)]
    public void FoldsPrimitiveOperations(string source, object expected)
    {
        CompilationResult result = CompileExpression(source);
        IrProgram program = RequireProgram(result);
        IrInstruction.Constant constant = GetInstructions(program)
            .OfType<IrInstruction.Constant>()
            .Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(constant.Value, Is.EqualTo(expected));
        }
    }

    [TestCase("infty + 5.0", double.PositiveInfinity)]
    [TestCase("-infty + 5.0", double.NegativeInfinity)]
    public void FoldsInfinityArithmetic(string source, double expected)
    {
        CompilationResult result = CompileExpression(source);
        IrProgram program = RequireProgram(result);
        IrInstruction.Constant constant = GetInstructions(program)
            .OfType<IrInstruction.Constant>()
            .Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(constant.Value, Is.EqualTo(expected));
        }
    }

    [Test]
    public void FoldsIndeterminateInfinityArithmeticToNan()
    {
        CompilationResult result = CompileExpression("infty + -infty");
        IrProgram program = RequireProgram(result);
        IrInstruction.Constant constant = GetInstructions(program)
            .OfType<IrInstruction.Constant>()
            .Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(constant.Value, Is.TypeOf<double>());
            Assert.That(double.IsNaN((double)constant.Value!), Is.True);
        }
    }

    [TestCase("\"\" + infty", "infty")]
    [TestCase("\"\" + -infty", "-infty")]
    [TestCase("\"\" + nan", "nan")]
    public void UsesLiteralSpellingForNonFiniteStringConversion(
        string source,
        string expected
    )
    {
        CompilationResult result = CompileExpression(source);
        IrProgram program = RequireProgram(result);
        IrInstruction.Constant constant = GetInstructions(program)
            .OfType<IrInstruction.Constant>()
            .Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(constant.Value, Is.EqualTo(expected));
        }
    }

    [TestCase("false && 1 / 0 == 0", false)]
    [TestCase("true ? 1 == 1 : 1 / 0 == 0", true)]
    [TestCase("(1 as int?) ?? 1 / 0", 1L)]
    public void DoesNotEvaluateDiscardedConstantOperands(
        string source,
        object expected
    )
    {
        CompilationResult result = CompileExpression(source);
        IrProgram program = RequireProgram(result);
        IrInstruction.Constant constant = GetInstructions(program)
            .OfType<IrInstruction.Constant>()
            .Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(constant.Value, Is.EqualTo(expected));
        }
    }

    [Test]
    public void DoesNotEvaluateDiscardedStatementBranch()
    {
        CompilationResult result = CompileProgram(
            "if (false) { var value = 1 / 0; }"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Program, Is.Not.Null);
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.ConstantEvaluationFailed)
            );
        }
    }

    [TestCase("1 / 0")]
    [TestCase("9223372036854775807 + 1")]
    [TestCase("1 << 64")]
    [TestCase("(1 as unknown) as string")]
    public void ReportsConstantEvaluationFailure(string source)
    {
        CompilationResult result = CompileExpression(source);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Program, Is.Null);
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Contain(DiagnosticCodes.ConstantEvaluationFailed)
            );
        }
    }

    [Test]
    public void FoldsConstantProviderArgumentsWithoutFoldingTheCall()
    {
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddFunction(
                "function.consume",
                "consume",
                [ new ParameterSymbol("value", TypeSymbols.Int) ],
                TypeSymbols.Int
            )
            .Build();
        CompilationResult result = MuLangCompiler.Compile(
            "consume(1 + 2)",
            environment,
            CompilationMode.Expression
        );
        IrProgram program = RequireProgram(result);
        IReadOnlyList<IrInstruction> instructions = GetInstructions(program);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                instructions.OfType<IrInstruction.Constant>().Single().Value,
                Is.EqualTo(3L)
            );
            Assert.That(
                instructions.OfType<IrInstruction.ProviderCall>(),
                Has.Exactly(1).Items
            );
        }
    }

    [Test]
    public void PreservesArrayCreationWhileFoldingItsElements()
    {
        CompilationResult result = CompileExpression("[1 + 2]");
        IrProgram program = RequireProgram(result);
        IReadOnlyList<IrInstruction> instructions = GetInstructions(program);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                instructions.OfType<IrInstruction.Constant>().Single().Value,
                Is.EqualTo(3L)
            );
            Assert.That(
                instructions.OfType<IrInstruction.CreateArray>(),
                Has.Exactly(1).Items
            );
        }
    }

    [Test]
    public void DisabledFeaturePreservesRuntimeExpressionsAndErrors()
    {
        LanguageProfile profile = new LanguageProfileBuilder()
            .WithConstantFolding(ConstantFoldingFeature.Disabled)
            .Build();
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        CompilationResult arithmetic = MuLangCompiler.Compile(
            "1 + 2 * 3",
            environment,
            CompilationMode.Expression,
            profile: profile
        );
        CompilationResult division = MuLangCompiler.Compile(
            "1 / 0",
            environment,
            CompilationMode.Expression,
            profile: profile
        );
        IrProgram arithmeticProgram = RequireProgram(arithmetic);
        IrProgram divisionProgram = RequireProgram(division);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                GetInstructions(arithmeticProgram)
                    .OfType<IrInstruction.Binary>()
                    .ToArray(),
                Has.Length.EqualTo(2)
            );
            Assert.That(
                GetInstructions(divisionProgram)
                    .OfType<IrInstruction.Binary>()
                    .ToArray(),
                Has.Length.EqualTo(1)
            );
            Assert.That(
                division.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Not.Contain(DiagnosticCodes.ConstantEvaluationFailed)
            );
        }
    }

    private static CompilationResult CompileExpression(
        string source,
        TypeSymbol? expectedType = null
    )
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();

        return MuLangCompiler.Compile(
            source,
            environment,
            CompilationMode.Expression,
            expectedType
        );
    }

    private static CompilationResult CompileProgram(string source)
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();

        return MuLangCompiler.Compile(
            source,
            environment,
            CompilationMode.Program,
            TypeSymbols.Void
        );
    }

    private static IrProgram RequireProgram(CompilationResult result)
    {
        return result.Program ??
            throw new AssertionException(
                $"Expected compilation to succeed: {FormatDiagnostics(result.Diagnostics)}"
            );
    }

    private static IReadOnlyList<IrInstruction> GetInstructions(IrProgram program)
    {
        return [ .. program.Blocks.SelectMany(static block => block.Instructions) ];
    }

    private static string FormatDiagnostics(DiagnosticCollection diagnostics)
    {
        return string.Join(
            Environment.NewLine,
            diagnostics.Select(
                static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"
            )
        );
    }
}
