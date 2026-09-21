using MuLang.Core;
using MuLang.Core.Diagnostics;

namespace MuLang.Compiler.Binding;

internal sealed record BindingResult(
    BoundRoot Root,
    DiagnosticCollection Diagnostics,
    CompilationMode CompilationMode,
    LanguageProfileFingerprint LanguageProfileFingerprint
);
