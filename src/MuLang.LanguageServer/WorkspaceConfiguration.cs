using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.LanguageServer;

internal sealed record WorkspaceConfiguration(
    CompilationMode CompilationMode,
    LanguageProfile Profile,
    EnvironmentSchema Environment,
    TypeSymbol? ExpectedResultType
)
{
    public static WorkspaceConfiguration Default { get; } = new (
        CompilationMode.Program,
        LanguageProfiles.Latest,
        new EnvironmentBuilder().Build(LanguageProfiles.Latest.LanguageVersion),
        null
    );
}
