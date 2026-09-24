using MuLang.Compiler.Binding;
using MuLang.Core.Diagnostics;

namespace MuLang.Compiler.Optimization;

internal sealed record ConstantFoldingResult(
    BindingResult Binding,
    DiagnosticCollection Diagnostics
);
