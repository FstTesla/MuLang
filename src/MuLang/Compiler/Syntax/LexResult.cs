using MuLang.Core.Diagnostics;

namespace MuLang.Compiler.Syntax;

internal sealed record LexResult(
    IReadOnlyList<SyntaxToken> Tokens,
    DiagnosticCollection Diagnostics
);
