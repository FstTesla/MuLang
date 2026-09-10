using MuLang.Core.Diagnostics;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed record LoweringResult(
    IrProgram? Program,
    DiagnosticCollection Diagnostics
);
