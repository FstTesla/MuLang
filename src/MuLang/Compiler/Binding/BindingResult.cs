using MuLang.Core.Diagnostics;

namespace MuLang.Compiler.Binding;

internal sealed record BindingResult(
    BoundRoot Root,
    DiagnosticCollection Diagnostics
);
