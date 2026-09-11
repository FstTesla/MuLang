using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.IR;

internal sealed record IrProgram(
    EnvironmentFingerprint EnvironmentFingerprint,
    TypeSymbol ResultType,
    int EntryBlock,
    IReadOnlyList<IrSlot> Slots,
    IReadOnlyList<IrBasicBlock> Blocks
);
