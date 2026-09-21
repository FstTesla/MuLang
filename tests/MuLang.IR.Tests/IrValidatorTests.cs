using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.IR.Tests;

public sealed class IrValidatorTests
{
    [Test]
    public void RejectsUnexpectedCompilationMetadata()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = new (
            environment.Fingerprint,
            CompilationMode.Expression,
            LanguageProfiles.Version1.Fingerprint,
            new IrFunction(
                "$entry",
                TypeSymbols.Int,
                0,
                [ new IrSlot(0, IrSlotKind.Temporary, TypeSymbols.Int, null) ],
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
                        ],
                        new IrTerminator.Return(default, 0)
                    ),
                ]
            ),
            [ ]
        );

        Assert.That(
            IrValidator.Validate(
                program,
                environment,
                CompilationMode.Program,
                program.LanguageProfileFingerprint
            ).Select(static diagnostic => diagnostic.Code),
            Does.Contain(IrDiagnosticCodes.CompilationMetadataMismatch)
        );
    }
}
