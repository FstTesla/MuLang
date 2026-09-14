using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

namespace MuLang.Tests.Compilation;

public sealed class MuLangCompilerTests
{
    [Test]
    public void CompilesAndExecutesExpressionThroughPublicFacade()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        CompilationResult result = MuLangCompiler.CompileExpression(
            "40 + 2",
            environment,
            TypeSymbols.Int
        );
        DotNetRuntimeContext context = new (
            environment,
            [ ],
            [ ]
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Delegate, Is.Not.Null);
        }

        Func<DotNetRuntimeContext, object?> compiled = result.Delegate ??
            throw new AssertionException("Expected a compiled delegate.");

        Assert.That(compiled(context), Is.EqualTo(42L));
    }

    [Test]
    public void ReturnsDiagnosticsWithoutDelegateForInvalidSource()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        CompilationResult result = MuLangCompiler.CompileProgram(
            "return missing;",
            environment,
            TypeSymbols.Int
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Delegate, Is.Null);
            Assert.That(result.Diagnostics.HasErrors, Is.True);
        }
    }

    [Test]
    public void RejectsVoidExpectedTypeAtThePublicBoundary()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();

        Assert.That(
            () => MuLangCompiler.CompileExpression(
                "1",
                environment,
                TypeSymbols.Void
            ),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("expectedType")
        );
    }

    [Test]
    public void PreservesExplicitNullExpectedTypeOverload()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();

        CompilationResult result = MuLangCompiler.CompileExpression(
            "1",
            environment,
            null
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }
}
