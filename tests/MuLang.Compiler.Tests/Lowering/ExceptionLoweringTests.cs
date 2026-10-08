using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Tests.Lowering;

public sealed class ExceptionLoweringTests
{
    [Test]
    public void LowersCombinedExceptionRegion()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build(
            LanguageVersion.Version1_2
        );
        CompilationResult result = MuLangCompiler.Compile(
            """
            try {
                throw { code = "failure", message = "Failure." };
            } catch {
                throw;
            } finally {
                var value = 1;
            }
            """,
            environment,
            CompilationMode.Program,
            TypeSymbols.Void,
            LanguageProfiles.Version1_2
        );
        IrFunction function = result.Program?.EntryFunction ??
            throw new AssertionException("Expected an IR program.");
        IrExceptionRegion region = function.ExceptionRegions.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics.HasErrors, Is.False);
            Assert.That(region.Handler, Is.Not.Null);
            Assert.That(region.Cleanup, Is.Not.Null);
            Assert.That(
                function.Blocks.Select(static block => block.Terminator),
                Has.Some.TypeOf<IrTerminator.Throw>()
            );
            Assert.That(
                function.Blocks.Select(static block => block.Terminator),
                Has.Some.TypeOf<IrTerminator.Resume>()
            );
            Assert.That(
                function.Slots[region.Handler!.ErrorSlot].Type,
                Is.SameAs(TypeSymbols.ErrorValue)
            );
        }
    }
}
