using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using MuLang.TestSupport;

namespace MuLang.Compiler.Tests.Language;

public sealed class LanguageProfileIrTests
{
    [Test]
    public void LoweringRetainsCompilationModeAndProfileFingerprint()
    {
        LanguageProfile profile = TestLanguageProfileFactory.Create(
            loops: LoopFeatures.For
        );
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From("40 + 2"),
            CompilationMode.Expression,
            profile
        );
        BindingResult binding = Binder.Bind(
            syntaxTree,
            environment,
            TypeSymbols.Int
        );
        LoweringResult lowering = Lowerer.Lower(binding, environment);
        IrProgram program = lowering.Program ??
            throw new AssertionException("Expected an IR program.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(program.CompilationMode, Is.EqualTo(CompilationMode.Expression));
            Assert.That(
                program.LanguageProfileFingerprint,
                Is.EqualTo(profile.Fingerprint)
            );
            Assert.That(
                program.EnvironmentFingerprint,
                Is.EqualTo(environment.Fingerprint)
            );
        }
    }

    [Test]
    public void ValidatorChecksExpectedCompilationMetadata()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        IrProgram program = LowerExpression(
            "1",
            environment,
            LanguageProfiles.Version1
        );
        LanguageProfile otherProfile = TestLanguageProfileFactory.Create(
            loops: LoopFeatures.For
        );
        DiagnosticCollection modeMismatch = IrValidator.Validate(
            program,
            environment,
            CompilationMode.Program,
            program.LanguageProfileFingerprint
        );
        DiagnosticCollection profileMismatch = IrValidator.Validate(
            program,
            environment,
            program.CompilationMode,
            otherProfile.Fingerprint
        );

        using (Assert.EnterMultipleScope())
        {
            AssertDiagnostic(
                modeMismatch,
                IrDiagnosticCodes.CompilationMetadataMismatch
            );
            AssertDiagnostic(
                profileMismatch,
                IrDiagnosticCodes.CompilationMetadataMismatch
            );
        }
    }

    [Test]
    public void EnvironmentAndLanguageProfileFingerprintsRemainIndependent()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        LanguageProfile firstProfile = LanguageProfiles.Version1;
        LanguageProfile secondProfile = TestLanguageProfileFactory.Create(
            loops: LoopFeatures.For
        );
        IrProgram first = LowerExpression("1", environment, firstProfile);
        IrProgram second = LowerExpression("1", environment, secondProfile);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                first.EnvironmentFingerprint,
                Is.EqualTo(second.EnvironmentFingerprint)
            );
            Assert.That(
                first.LanguageProfileFingerprint,
                Is.Not.EqualTo(second.LanguageProfileFingerprint)
            );
        }
    }

    private static IrProgram LowerExpression(
        string source,
        EnvironmentSchema environment,
        LanguageProfile profile
    )
    {
        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Expression,
            profile
        );
        BindingResult binding = Binder.Bind(
            syntaxTree,
            environment,
            TypeSymbols.Int
        );
        Assert.That(binding.Diagnostics, Is.Empty);
        LoweringResult lowering = Lowerer.Lower(binding, environment);
        Assert.That(lowering.Diagnostics, Is.Empty);

        return lowering.Program ??
            throw new AssertionException("Expected an IR program.");
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
