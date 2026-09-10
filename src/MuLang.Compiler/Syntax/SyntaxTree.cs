using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record SyntaxTree(
    SourceText Source,
    CompilationMode CompilationMode,
    RootSyntax Root,
    DiagnosticCollection Diagnostics
);
