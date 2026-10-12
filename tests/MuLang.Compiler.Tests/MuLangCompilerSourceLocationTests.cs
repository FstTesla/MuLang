using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests;

public sealed class MuLangCompilerSourceLocationTests
{
    [Test]
    public void CompilationCarriesSourceDocumentAndLineMap()
    {
        CompilationResult result = MuLangCompiler.Compile(
            "return 1;\n",
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            new MuLangCompilationOptions
            {
                ExpectedResultType = TypeSymbols.Int,
                Profile = LanguageProfiles.Version1_2,
                SourceDocument = "file:///sample.mu",
            }
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.SourceDocument, Is.EqualTo("file:///sample.mu"));
            Assert.That(result.LineMap.LineStarts, Is.EqualTo([ 0, 10 ]));
            Assert.That(
                result.Program?.Attributes.Program?.SourceDocument,
                Is.EqualTo("file:///sample.mu")
            );
            Assert.That(
                result.Program?.Attributes.Program?.LineMap?.LineStarts,
                Is.EqualTo([ 0, 10 ])
            );
        }
    }
}
