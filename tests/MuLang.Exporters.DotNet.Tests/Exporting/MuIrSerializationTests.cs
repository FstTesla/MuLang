using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.IR;
using MuLang.IR.Serialization;

namespace MuLang.Exporters.DotNet.Tests.Exporting;

public sealed class MuIrSerializationTests
{
    [Test]
    public void DeserializedCompilerProgramValidatesExportsAndExecutes()
    {
        EnvironmentSchema environment = new EnvironmentBuilder().Build();
        CompilationResult compilation = MuLangCompiler.Compile(
            "40 + 2",
            environment,
            CompilationMode.Expression,
            TypeSymbols.Int
        );
        IrProgram original = compilation.Program ??
            throw new InvalidOperationException("Compilation failed.");
        string text = MuIrWriter.WriteToString(original);

        MuIrReadResult read = MuIrReader.Read(text);
        IrProgram deserialized = read.Program ??
            throw new InvalidOperationException("MuIR reading failed.");
        DotNetExportResult originalExport = DotNetExporter.Export(
            original,
            environment,
            CompilationMode.Expression,
            LanguageProfiles.Version1_1.Fingerprint
        );
        DotNetExportResult deserializedExport = DotNetExporter.Export(
            deserialized,
            environment,
            CompilationMode.Expression,
            LanguageProfiles.Version1_1.Fingerprint
        );
        DotNetRuntimeContext context = new (environment, [ ], [ ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read.Success, Is.True);
            Assert.That(IrValidator.Validate(deserialized, environment).HasErrors, Is.False);
            Assert.That(MuIrWriter.WriteToString(deserialized), Is.EqualTo(text));
            Assert.That(originalExport.Diagnostics.HasErrors, Is.False);
            Assert.That(deserializedExport.Diagnostics.HasErrors, Is.False);
            Assert.That(originalExport.Delegate!(context), Is.EqualTo(42L));
            Assert.That(deserializedExport.Delegate!(context), Is.EqualTo(42L));
        }
    }
}
